using HireFlow.Domain.Applications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.Companies;
using HireFlow.Domain.Interviews;
using HireFlow.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HireFlow.Infrastructure.Persistence;

/// <summary>Creates the schema on startup and, when the database is empty, loads demo data.</summary>
public sealed class DatabaseInitializer(HireFlowDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(bool seed, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!seed || await db.Companies.AnyAsync(cancellationToken))
        {
            return;
        }

        Seed();
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded demo data: {Companies} companies, {Jobs} jobs, {Candidates} candidates",
            db.Companies.Local.Count, db.Jobs.Local.Count, db.Candidates.Local.Count);
    }

    private void Seed()
    {
        var northwind = new Company("Northwind Labs", "https://northwind.example", "Developer Tools", "Chișinău, MD");
        var bluefin = new Company("Bluefin Analytics", "https://bluefin.example", "Data & AI", "Bucharest, RO");
        var orbit = new Company("Orbit Commerce", "https://orbit.example", "E-commerce", "Remote (EU)");
        db.Companies.AddRange(northwind, bluefin, orbit);

        var backend = new JobPosting(northwind.Id, "Senior .NET Engineer",
            "Own our GraphQL gateway and the services behind it. You will design schemas, tune EF Core queries and mentor two engineers.",
            "Chișinău, MD", WorkMode.Hybrid, EmploymentType.FullTime,
            new SalaryRange(42_000, 60_000, "EUR"), ["C#", ".NET", "GraphQL", "PostgreSQL"]);
        var fullstack = new JobPosting(northwind.Id, "Full-Stack Developer (.NET + React)",
            "Ship features end to end across our React front end and ASP.NET Core APIs.",
            "Chișinău, MD", WorkMode.Onsite, EmploymentType.FullTime,
            new SalaryRange(30_000, 45_000, "EUR"), ["C#", "React", "TypeScript"]);
        var dataEng = new JobPosting(bluefin.Id, "Data Platform Engineer",
            "Build ingestion pipelines and the APIs our analysts query every day.",
            "Bucharest, RO", WorkMode.Remote, EmploymentType.FullTime,
            new SalaryRange(50_000, 70_000, "EUR"), ["Python", "SQL", "Kafka"]);
        var intern = new JobPosting(orbit.Id, "Backend Intern",
            "Six-month internship working on order processing in .NET.",
            null, WorkMode.Remote, EmploymentType.Internship, requiredSkills: ["C#"]);
        var contractor = new JobPosting(orbit.Id, "GraphQL Consultant",
            "Three-month contract to migrate our REST storefront API to GraphQL.",
            null, WorkMode.Remote, EmploymentType.Contract,
            new SalaryRange(6_000, 9_000, "EUR"), ["GraphQL", "Hot Chocolate"]);

        foreach (var job in new[] { backend, fullstack, dataEng, intern, contractor })
        {
            job.Publish();
        }

        contractor.Close();
        var draft = new JobPosting(bluefin.Id, "ML Engineer",
            "Draft posting, not yet published.", "Bucharest, RO", WorkMode.Hybrid, EmploymentType.FullTime);
        db.Jobs.AddRange(backend, fullstack, dataEng, intern, contractor, draft);

        var ana = new Candidate("Ana Popescu", "ana.popescu@example.com", "Backend engineer, 6 years in .NET", 6, ["C#", ".NET", "SQL"]);
        var ion = new Candidate("Ion Rusu", "ion.rusu@example.com", "Full-stack developer", 4, ["React", "C#", "TypeScript"]);
        var elena = new Candidate("Elena Ciobanu", "elena.ciobanu@example.com", "Data engineer", 5, ["Python", "Kafka", "SQL"]);
        var mihai = new Candidate("Mihai Lungu", "mihai.lungu@example.com", "CS student", 0, ["C#"]);
        db.Candidates.AddRange(ana, ion, elena, mihai);

        var anaBackend = JobApplication.Submit(backend, ana, "I've been building GraphQL APIs on Hot Chocolate since v12.");
        anaBackend.MoveTo(ApplicationStage.Screening, "Strong CV");
        anaBackend.MoveTo(ApplicationStage.Interview);

        var ionFullstack = JobApplication.Submit(fullstack, ion);
        ionFullstack.MoveTo(ApplicationStage.Screening);

        var ionBackend = JobApplication.Submit(backend, ion);
        ionBackend.MoveTo(ApplicationStage.Rejected, "Looking for more backend depth");

        var elenaData = JobApplication.Submit(dataEng, elena);
        elenaData.MoveTo(ApplicationStage.Screening);
        elenaData.MoveTo(ApplicationStage.Interview);
        elenaData.MoveTo(ApplicationStage.Offer, "Offer sent: 62k EUR");

        var mihaiIntern = JobApplication.Submit(intern, mihai);
        db.Applications.AddRange(anaBackend, ionFullstack, ionBackend, elenaData, mihaiIntern);

        var technical = Interview.Schedule(anaBackend, InterviewKind.Technical, DateTime.UtcNow.AddDays(2), 60, "Victor Moraru");
        var screen = Interview.Schedule(anaBackend, InterviewKind.PhoneScreen, DateTime.UtcNow.AddDays(-3), 30, "Irina Balan");
        screen.SubmitFeedback(4, "Clear communicator, solid fundamentals.");
        var onsite = Interview.Schedule(elenaData, InterviewKind.Onsite, DateTime.UtcNow.AddDays(-5), 180, "Radu Stoica");
        onsite.SubmitFeedback(5, "Best data-modelling answers this quarter.");
        db.Interviews.AddRange(technical, screen, onsite);
    }
}
