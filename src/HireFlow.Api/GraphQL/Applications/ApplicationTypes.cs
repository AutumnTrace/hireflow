using HireFlow.Api.Auth;
using HireFlow.Domain.Applications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.Common;
using HireFlow.Domain.Interviews;
using HireFlow.Domain.Jobs;
using HireFlow.Infrastructure.Persistence;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Api.GraphQL.Applications;

[QueryType]
public static class ApplicationQueries
{
    [Authorize]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<JobApplication> GetApplications(HireFlowDbContext db)
        => db.Applications.AsNoTracking().OrderByDescending(a => a.UpdatedAt);

    [Authorize]
    public static async Task<JobApplication?> GetApplicationAsync(Guid id, IApplicationByIdDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync(id, ct);
}

[MutationType]
public static class ApplicationMutations
{
    [Authorize(Roles = [Roles.Recruiter, Roles.Admin])]
    [Error<EntityNotFoundException>]
    [Error<JobStatusException>]
    [Error<DuplicateApplicationException>]
    [Error<DomainValidationException>]
    public static async Task<JobApplication> SubmitApplicationAsync(
        Guid jobPostingId,
        Guid candidateId,
        string? coverLetter,
        HireFlowDbContext db,
        ITopicEventSender events,
        CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([jobPostingId], ct)
            ?? throw new EntityNotFoundException(nameof(JobPosting), jobPostingId);
        var candidate = await db.Candidates.FindAsync([candidateId], ct)
            ?? throw new EntityNotFoundException(nameof(Candidate), candidateId);

        if (await db.Applications.AnyAsync(a => a.JobPostingId == jobPostingId && a.CandidateId == candidateId, ct))
        {
            throw new DuplicateApplicationException(jobPostingId, candidateId);
        }

        var application = JobApplication.Submit(job, candidate, coverLetter);
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);

        await events.SendAsync(Topics.ApplicationSubmitted(jobPostingId), application, ct);
        return application;
    }

    /// <summary>Moves an application through the pipeline. Illegal jumps (e.g. Applied → Hired) are rejected.</summary>
    [Authorize(Roles = [Roles.Recruiter, Roles.Admin])]
    [Error<EntityNotFoundException>]
    [Error<InvalidStageTransitionException>]
    [Error<DomainValidationException>]
    public static async Task<JobApplication> MoveApplicationAsync(
        Guid applicationId,
        ApplicationStage stage,
        string? note,
        HireFlowDbContext db,
        ITopicEventSender events,
        CancellationToken ct)
    {
        var application = await db.Applications.FindAsync([applicationId], ct)
            ?? throw new EntityNotFoundException(nameof(JobApplication), applicationId);

        var transition = application.MoveTo(stage, note);
        db.StageTransitions.Add(transition);
        await db.SaveChangesAsync(ct);

        await events.SendAsync(
            Topics.StageChanged(application.JobPostingId),
            new ApplicationStageChanged(application.Id, application.JobPostingId, transition.From, transition.To, transition.Note, transition.ChangedAt),
            ct);
        return application;
    }
}

[ObjectType<JobApplication>]
public static partial class JobApplicationNode
{
    static partial void Configure(IObjectTypeDescriptor<JobApplication> descriptor)
        => descriptor.Ignore(a => a.History);

    public static async Task<JobPosting> GetJobAsync(
        [Parent] JobApplication application, IJobByIdDataLoader loader, CancellationToken ct)
        => await loader.LoadRequiredAsync(application.JobPostingId, ct);

    public static async Task<Candidate> GetCandidateAsync(
        [Parent] JobApplication application, ICandidateByIdDataLoader loader, CancellationToken ct)
        => await loader.LoadRequiredAsync(application.CandidateId, ct);

    /// <summary>Every stage this application has been through, oldest first.</summary>
    public static async Task<IReadOnlyList<StageTransition>> GetHistoryAsync(
        [Parent] JobApplication application, IHistoryByApplicationIdDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync(application.Id, ct) ?? [];

    public static async Task<IReadOnlyList<Interview>> GetInterviewsAsync(
        [Parent] JobApplication application, IInterviewsByApplicationIdDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync(application.Id, ct) ?? [];

    /// <summary>Stages this application may legally move to next — handy for rendering UI buttons.</summary>
    public static IReadOnlyList<ApplicationStage> GetAllowedNextStages([Parent] JobApplication application)
        => ApplicationStageRules.AllowedNext(application.Stage);
}
