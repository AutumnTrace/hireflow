using HireFlow.Domain.Applications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.Companies;
using HireFlow.Domain.Interviews;
using HireFlow.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Infrastructure.Persistence;

public sealed class HireFlowDbContext(DbContextOptions<HireFlowDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<JobPosting> Jobs => Set<JobPosting>();

    public DbSet<Candidate> Candidates => Set<Candidate>();

    public DbSet<JobApplication> Applications => Set<JobApplication>();

    public DbSet<StageTransition> StageTransitions => Set<StageTransition>();

    public DbSet<Interview> Interviews => Set<Interview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(HireFlowDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable strings rather than magic numbers.
        configurationBuilder.Properties<ApplicationStage>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<JobStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<WorkMode>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<EmploymentType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<InterviewKind>().HaveConversion<string>().HaveMaxLength(20);
    }
}
