using HireFlow.Api.Auth;
using HireFlow.Domain.Applications;
using HireFlow.Domain.Common;
using HireFlow.Domain.Interviews;
using HireFlow.Infrastructure.Persistence;
using HotChocolate.Authorization;

namespace HireFlow.Api.GraphQL.Interviews;

[MutationType]
public static class InterviewMutations
{
    [Authorize(Roles = [Roles.Recruiter, Roles.Admin])]
    [Error<EntityNotFoundException>]
    [Error<ApplicationClosedException>]
    [Error<DomainValidationException>]
    public static async Task<Interview> ScheduleInterviewAsync(
        Guid applicationId,
        InterviewKind kind,
        DateTime scheduledAt,
        int durationMinutes,
        string interviewer,
        HireFlowDbContext db,
        CancellationToken ct)
    {
        var application = await db.Applications.FindAsync([applicationId], ct)
            ?? throw new EntityNotFoundException(nameof(JobApplication), applicationId);

        var interview = Interview.Schedule(application, kind, scheduledAt.ToUniversalTime(), durationMinutes, interviewer);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync(ct);
        return interview;
    }

    [Authorize(Roles = [Roles.Recruiter, Roles.Admin])]
    [Error<EntityNotFoundException>]
    [Error<DomainValidationException>]
    public static async Task<Interview> SubmitInterviewFeedbackAsync(
        Guid interviewId,
        int rating,
        string? feedback,
        HireFlowDbContext db,
        CancellationToken ct)
    {
        var interview = await db.Interviews.FindAsync([interviewId], ct)
            ?? throw new EntityNotFoundException(nameof(Interview), interviewId);

        interview.SubmitFeedback(rating, feedback);
        await db.SaveChangesAsync(ct);
        return interview;
    }
}

[ObjectType<Interview>]
public static partial class InterviewNode
{
    public static async Task<JobApplication> GetApplicationAsync(
        [Parent] Interview interview, IApplicationByIdDataLoader loader, CancellationToken ct)
        => await loader.LoadRequiredAsync(interview.ApplicationId, ct);
}
