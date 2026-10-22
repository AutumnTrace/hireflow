using HireFlow.Domain.Applications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.Companies;
using HireFlow.Domain.Interviews;
using HireFlow.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Website).HasMaxLength(200);
        builder.Property(c => c.Industry).HasMaxLength(80);
        builder.Property(c => c.Location).HasMaxLength(120);
        builder.HasIndex(c => c.Name).IsUnique();
    }
}

internal sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Title).HasMaxLength(120).IsRequired();
        builder.Property(j => j.Description).HasMaxLength(8000).IsRequired();
        builder.Property(j => j.Location).HasMaxLength(120);
        builder.Property(j => j.RequiredSkills);

        builder.OwnsOne(j => j.Salary, salary =>
        {
            salary.Property(s => s.Min).HasColumnName("SalaryMin");
            salary.Property(s => s.Max).HasColumnName("SalaryMax");
            salary.Property(s => s.Currency).HasColumnName("SalaryCurrency").HasMaxLength(3);
        });

        builder.HasOne<Company>().WithMany().HasForeignKey(j => j.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(j => new { j.Status, j.PublishedAt });
    }
}

internal sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.FullName).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(254).IsRequired();
        builder.Property(c => c.Headline).HasMaxLength(160);
        builder.Property(c => c.Skills);
        builder.HasIndex(c => c.Email).IsUnique();
    }
}

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.CoverLetter).HasMaxLength(4000);
        builder.Ignore(a => a.IsActive);

        builder.HasOne<JobPosting>().WithMany().HasForeignKey(a => a.JobPostingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Candidate>().WithMany().HasForeignKey(a => a.CandidateId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.History)
            .WithOne()
            .HasForeignKey(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);

        // One application per candidate per job, enforced by the database as the last line of defence.
        builder.HasIndex(a => new { a.JobPostingId, a.CandidateId }).IsUnique();
        builder.HasIndex(a => a.Stage);
    }
}

internal sealed class StageTransitionConfiguration : IEntityTypeConfiguration<StageTransition>
{
    public void Configure(EntityTypeBuilder<StageTransition> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Note).HasMaxLength(1000);
        builder.HasIndex(t => new { t.ApplicationId, t.ChangedAt });
    }
}

internal sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Interviewer).HasMaxLength(120).IsRequired();
        builder.Property(i => i.Feedback).HasMaxLength(4000);
        builder.HasOne<JobApplication>().WithMany().HasForeignKey(i => i.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => new { i.ApplicationId, i.ScheduledAt });
    }
}
