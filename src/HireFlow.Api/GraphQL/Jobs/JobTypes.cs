using System.Security.Claims;
using HireFlow.Api.Auth;
using HireFlow.Domain.Applications;
using HireFlow.Domain.Common;
using HireFlow.Domain.Companies;
using HireFlow.Domain.Jobs;
using HireFlow.Infrastructure.Persistence;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Api.GraphQL.Jobs;

[QueryType]
public static class JobQueries
{
    /// <summary>Job board. Anonymous callers only ever see open postings.</summary>
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<JobPosting> GetJobs(HireFlowDbContext db, ClaimsPrincipal user)
    {
        var jobs = db.Jobs.AsNoTracking();
        if (user.Identity?.IsAuthenticated != true)
        {
            jobs = jobs.Where(j => j.Status == JobStatus.Open);
        }

        return jobs.OrderByDescending(j => j.PublishedAt).ThenBy(j => j.Title);
    }

    public static async Task<JobPosting?> GetJobAsync(Guid id, IJobByIdDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync(id, ct);
}

public sealed record SalaryRangeInput(int Min, int Max, string Currency);

[MutationType]
public static class JobMutations
{
    [Authorize(Roles = [Roles.Recruiter, Roles.Admin])]
    [Error<DomainValidationException>]
    [Error<EntityNotFoundException>]
    public static async Task<JobPosting> CreateJobPostingAsync(
        Guid companyId,
        string title,
        string description,
        string? location,
        WorkMode workMode,
        EmploymentType employmentType,
        SalaryRangeInput? salary,
        IReadOnlyList<string>? requiredSkills,
        HireFlowDbContext db,
        CancellationToken ct)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == companyId, ct))
        {
            throw new EntityNotFoundException(nameof(Company), companyId);
        }

        var job = new JobPosting(
            companyId,
            title,
            description,
            location,
            workMode,
            employmentType,
            salary is null ? null : new SalaryRange(salary.Min, salary.Max, salary.Currency),
            requiredSkills);

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    [Authorize(Roles = [Roles.Recruiter, Roles.Admin])]
    [Error<EntityNotFoundException>]
    [Error<JobStatusException>]
    public static async Task<JobPosting> PublishJobPostingAsync(Guid id, HireFlowDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct) ?? throw new EntityNotFoundException("JobPosting", id);
        job.Publish();
        await db.SaveChangesAsync(ct);
        return job;
    }

    [Authorize(Roles = [Roles.Recruiter, Roles.Admin])]
    [Error<EntityNotFoundException>]
    [Error<JobStatusException>]
    public static async Task<JobPosting> CloseJobPostingAsync(Guid id, HireFlowDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct) ?? throw new EntityNotFoundException("JobPosting", id);
        job.Close();
        await db.SaveChangesAsync(ct);
        return job;
    }
}

[ObjectType<JobPosting>]
public static partial class JobPostingNode
{
    public static async Task<Company> GetCompanyAsync(
        [Parent] JobPosting job, ICompanyByIdDataLoader loader, CancellationToken ct)
        => await loader.LoadRequiredAsync(job.CompanyId, ct);

    [Authorize]
    public static async Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(
        [Parent] JobPosting job,
        ApplicationStage? stage,
        IApplicationsByJobIdDataLoader loader,
        CancellationToken ct)
    {
        var applications = await loader.LoadAsync(job.Id, ct) ?? [];
        return stage is null ? applications : applications.Where(a => a.Stage == stage).ToList();
    }

    /// <summary>Hiring funnel for this posting: totals per stage and the hire rate.</summary>
    [Authorize]
    public static async Task<PipelineStats> GetPipelineAsync(
        [Parent] JobPosting job, IPipelineByJobIdDataLoader loader, CancellationToken ct)
        => await loader.LoadRequiredAsync(job.Id, ct);
}
