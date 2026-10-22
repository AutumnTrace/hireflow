using HireFlow.Api.Auth;
using HireFlow.Domain.Applications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.Common;
using HireFlow.Domain.Jobs;
using HireFlow.Infrastructure.Persistence;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Api.GraphQL.Candidates;

[QueryType]
public static class CandidateQueries
{
    /// <summary>Candidate profiles contain personal data, so they require an authenticated caller.</summary>
    [Authorize]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Candidate> GetCandidates(HireFlowDbContext db)
        => db.Candidates.AsNoTracking().OrderBy(c => c.FullName);

    [Authorize]
    public static async Task<Candidate?> GetCandidateAsync(Guid id, ICandidateByIdDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync(id, ct);
}

[MutationType]
public static class CandidateMutations
{
    [Authorize(Roles = [Roles.Recruiter, Roles.Admin])]
    [Error<DomainValidationException>]
    [Error<DuplicateCandidateException>]
    public static async Task<Candidate> RegisterCandidateAsync(
        string fullName,
        string email,
        string? headline,
        int yearsOfExperience,
        IReadOnlyList<string>? skills,
        HireFlowDbContext db,
        CancellationToken ct)
    {
        var candidate = new Candidate(fullName, email, headline, yearsOfExperience, skills);
        if (await db.Candidates.AnyAsync(c => c.Email == candidate.Email, ct))
        {
            throw new DuplicateCandidateException(candidate.Email);
        }

        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(ct);
        return candidate;
    }
}

[ObjectType<Candidate>]
public static partial class CandidateNode
{
    public static async Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(
        [Parent] Candidate candidate, IApplicationsByCandidateIdDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync(candidate.Id, ct) ?? [];

    /// <summary>Open postings ranked by skill match, excluding ones the candidate already applied to.</summary>
    public static async Task<IReadOnlyList<JobRecommendation>> GetRecommendedJobsAsync(
        [Parent] Candidate candidate,
        HireFlowDbContext db,
        IApplicationsByCandidateIdDataLoader applications,
        CancellationToken ct,
        int limit = 5)
    {
        var appliedTo = (await applications.LoadAsync(candidate.Id, ct) ?? [])
            .Select(a => a.JobPostingId)
            .ToHashSet();

        var openJobs = await db.Jobs.AsNoTracking()
            .Where(j => j.Status == JobStatus.Open)
            .ToListAsync(ct);

        return openJobs
            .Where(j => !appliedTo.Contains(j.Id))
            .Select(j => new JobRecommendation(j, SkillMatch.Evaluate(j.RequiredSkills, candidate.Skills)))
            .Where(r => r.Match.MatchedSkills.Count > 0)
            .OrderByDescending(r => r.Match.Score)
            .ThenByDescending(r => r.Match.MatchedSkills.Count)
            .Take(Math.Clamp(limit, 1, 20))
            .ToList();
    }
}

public sealed record JobRecommendation(JobPosting Job, SkillMatch Match);
