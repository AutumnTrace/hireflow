using HireFlow.Domain.Common;

namespace HireFlow.Domain.Jobs;

public sealed class JobPosting
{
    private JobPosting()
    {
    }

    public JobPosting(
        Guid companyId,
        string title,
        string description,
        string? location,
        WorkMode workMode,
        EmploymentType employmentType,
        SalaryRange? salary = null,
        IEnumerable<string>? requiredSkills = null)
    {
        CompanyId = companyId;
        Title = Guard.NotBlank(title, nameof(title), 120);
        Description = Guard.NotBlank(description, nameof(description), 8000);
        Location = Guard.Optional(location, nameof(location), 120);
        WorkMode = workMode;
        EmploymentType = employmentType;
        Salary = salary;
        RequiredSkills = Guard.Tags(requiredSkills);
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CompanyId { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public string? Location { get; private set; }

    public WorkMode WorkMode { get; private set; }

    public EmploymentType EmploymentType { get; private set; }

    public SalaryRange? Salary { get; private set; }

    public List<string> RequiredSkills { get; private set; } = [];

    public JobStatus Status { get; private set; } = JobStatus.Draft;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public DateTime? PublishedAt { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public void Publish()
    {
        if (Status != JobStatus.Draft)
        {
            throw new JobStatusException(Id, Status, "Only draft postings can be published.");
        }

        Status = JobStatus.Open;
        PublishedAt = DateTime.UtcNow;
    }

    public void Close()
    {
        if (Status != JobStatus.Open)
        {
            throw new JobStatusException(Id, Status, "Only open postings can be closed.");
        }

        Status = JobStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }

    public void EnsureAcceptingApplications()
    {
        if (Status != JobStatus.Open)
        {
            throw new JobStatusException(Id, Status, "This posting is not accepting applications.");
        }
    }
}

/// <summary>An operation is not allowed in the posting's current lifecycle status.</summary>
public sealed class JobStatusException(Guid jobId, JobStatus status, string message) : DomainException(message)
{
    public Guid JobId { get; } = jobId;

    public JobStatus Status { get; } = status;
}
