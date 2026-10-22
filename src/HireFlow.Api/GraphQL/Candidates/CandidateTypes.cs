using HireFlow.Api.Auth;
using HireFlow.Domain.Applications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.Common;
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
}
