using HireFlow.Api.Auth;
using HireFlow.Domain.Applications;
using HireFlow.Domain.Common;
using HireFlow.Domain.Interviews;
using HireFlow.Infrastructure.Persistence;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Api.GraphQL.Interviews;

[QueryType]
public static class InterviewQueries
{
    /// <summary>All interviews, with filtering (e.g. by interviewer or date range) and sorting.</summary>
    [Authorize]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Interview> GetInterviews(HireFlowDbContext db)
        => db.Interviews.AsNoTracking().OrderBy(i => i.ScheduledAt);

    /// <summary>The interviewer's agenda: interviews starting within the next <paramref name="days"/> days, soonest first.</summary>
    [Authorize]
    public static async Task<IReadOnlyList<Interview>> GetUpcomingInterviewsAsync(
        HireFlowDbContext db,
        TimeProvider clock,
        CancellationToken ct,
        int days = 7,
        string? interviewer = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var until = now.AddDays(Math.Clamp(days, 1, 90));

        var query = db.Interviews.AsNoTracking().Where(i => i.ScheduledAt >= now && i.ScheduledAt < until);
        if (!string.IsNullOrWhiteSpace(interviewer))
        {
            query = query.Where(i => i.Interviewer == interviewer.Trim());
        }

        return await query.OrderBy(i => i.ScheduledAt).Take(100).ToListAsync(ct);
    }
}

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
