using HireFlow.Api.Auth;
using HireFlow.Domain.Common;
using HireFlow.Domain.Companies;
using HireFlow.Domain.Jobs;
using HireFlow.Infrastructure.Persistence;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Api.GraphQL.Companies;

[QueryType]
public static class CompanyQueries
{
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Company> GetCompanies(HireFlowDbContext db)
        => db.Companies.AsNoTracking().OrderBy(c => c.Name);

    public static async Task<Company?> GetCompanyAsync(Guid id, ICompanyByIdDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync(id, ct);
}

[MutationType]
public static class CompanyMutations
{
    [Authorize(Roles = [Roles.Recruiter, Roles.Admin])]
    [Error<DomainValidationException>]
    [Error<DuplicateCompanyException>]
    public static async Task<Company> CreateCompanyAsync(
        string name,
        string? website,
        string? industry,
        string? location,
        HireFlowDbContext db,
        CancellationToken ct)
    {
        var company = new Company(name, website, industry, location);
        if (await db.Companies.AnyAsync(c => c.Name == company.Name, ct))
        {
            throw new DuplicateCompanyException(company.Name);
        }

        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
        return company;
    }
}

[ObjectType<Company>]
public static partial class CompanyNode
{
    public static async Task<IReadOnlyList<JobPosting>> GetJobsAsync(
        [Parent] Company company,
        JobStatus? status,
        IJobsByCompanyIdDataLoader loader,
        CancellationToken ct)
    {
        var jobs = await loader.LoadAsync(company.Id, ct) ?? [];
        return status is null ? jobs : jobs.Where(j => j.Status == status).ToList();
    }
}

public sealed class DuplicateCompanyException(string name)
    : DomainException($"A company named '{name}' already exists.")
{
    public string Name { get; } = name;
}
