namespace HireFlow.Domain.Jobs;

public enum JobStatus
{
    Draft,
    Open,
    Closed,
}

public enum WorkMode
{
    Onsite,
    Hybrid,
    Remote,
}

public enum EmploymentType
{
    FullTime,
    PartTime,
    Contract,
    Internship,
}
