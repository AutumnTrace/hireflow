using HireFlow.Domain.Applications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.Companies;
using HireFlow.Domain.Interviews;
using HireFlow.Domain.Jobs;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Api.GraphQL;

/// <summary>
/// Batched lookups for every relationship in the graph. A query for 50 jobs and their
/// companies issues 2 SQL statements instead of 51 (the classic N+1 problem).
/// Hot Chocolate's source generator turns each method into a typed DataLoader.
/// </summary>
internal static class DataLoaders
{
    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, Company>> GetCompanyByIdAsync(
        IReadOnlyList<Guid> ids, HireFlowDbContext db, CancellationToken ct)
        => await db.Companies.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, JobPosting>> GetJobByIdAsync(
        IReadOnlyList<Guid> ids, HireFlowDbContext db, CancellationToken ct)
        => await db.Jobs.AsNoTracking().Where(j => ids.Contains(j.Id)).ToDictionaryAsync(j => j.Id, ct);

    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, Candidate>> GetCandidateByIdAsync(
        IReadOnlyList<Guid> ids, HireFlowDbContext db, CancellationToken ct)
        => await db.Candidates.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, JobApplication>> GetApplicationByIdAsync(
        IReadOnlyList<Guid> ids, HireFlowDbContext db, CancellationToken ct)
        => await db.Applications.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

    [DataLoader]
    public static async Task<ILookup<Guid, JobPosting>> GetJobsByCompanyIdAsync(
        IReadOnlyList<Guid> companyIds, HireFlowDbContext db, CancellationToken ct)
        => (await db.Jobs.AsNoTracking()
                .Where(j => companyIds.Contains(j.CompanyId))
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync(ct))
            .ToLookup(j => j.CompanyId);

    [DataLoader]
    public static async Task<ILookup<Guid, JobApplication>> GetApplicationsByJobIdAsync(
        IReadOnlyList<Guid> jobIds, HireFlowDbContext db, CancellationToken ct)
        => (await db.Applications.AsNoTracking()
                .Where(a => jobIds.Contains(a.JobPostingId))
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync(ct))
            .ToLookup(a => a.JobPostingId);

    [DataLoader]
    public static async Task<ILookup<Guid, JobApplication>> GetApplicationsByCandidateIdAsync(
        IReadOnlyList<Guid> candidateIds, HireFlowDbContext db, CancellationToken ct)
        => (await db.Applications.AsNoTracking()
                .Where(a => candidateIds.Contains(a.CandidateId))
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync(ct))
            .ToLookup(a => a.CandidateId);

    [DataLoader]
    public static async Task<ILookup<Guid, StageTransition>> GetHistoryByApplicationIdAsync(
        IReadOnlyList<Guid> applicationIds, HireFlowDbContext db, CancellationToken ct)
        => (await db.StageTransitions.AsNoTracking()
                .Where(t => applicationIds.Contains(t.ApplicationId))
                .OrderBy(t => t.ChangedAt)
                .ToListAsync(ct))
            .ToLookup(t => t.ApplicationId);

    [DataLoader]
    public static async Task<ILookup<Guid, Interview>> GetInterviewsByApplicationIdAsync(
        IReadOnlyList<Guid> applicationIds, HireFlowDbContext db, CancellationToken ct)
        => (await db.Interviews.AsNoTracking()
                .Where(i => applicationIds.Contains(i.ApplicationId))
                .OrderBy(i => i.ScheduledAt)
                .ToListAsync(ct))
            .ToLookup(i => i.ApplicationId);

    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, PipelineStats>> GetPipelineByJobIdAsync(
        IReadOnlyList<Guid> jobIds, HireFlowDbContext db, CancellationToken ct)
    {
        var rows = await db.Applications.AsNoTracking()
            .Where(a => jobIds.Contains(a.JobPostingId))
            .GroupBy(a => new { a.JobPostingId, a.Stage })
            .Select(g => new { g.Key.JobPostingId, g.Key.Stage, Count = g.Count() })
            .ToListAsync(ct);

        return jobIds.ToDictionary(
            id => id,
            id => PipelineStats.From(rows.Where(r => r.JobPostingId == id).ToDictionary(r => r.Stage, r => r.Count)));
    }
}
