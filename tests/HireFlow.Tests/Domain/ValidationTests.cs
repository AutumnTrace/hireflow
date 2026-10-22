using HireFlow.Domain.Applications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.Common;
using HireFlow.Domain.Interviews;
using HireFlow.Domain.Jobs;

namespace HireFlow.Tests.Domain;

public sealed class ValidationTests
{
    [Fact]
    public void Salary_max_must_not_be_below_min()
    {
        var ex = Assert.Throws<DomainValidationException>(() => new SalaryRange(50_000, 40_000, "EUR"));
        Assert.Equal("max", ex.Field);
    }

    [Theory]
    [InlineData("EURO")]
    [InlineData("E")]
    public void Currency_must_be_iso_code(string currency)
        => Assert.Throws<DomainValidationException>(() => new SalaryRange(1, 2, currency));

    [Fact]
    public void Currency_is_normalised_to_upper_case()
        => Assert.Equal("EUR", new SalaryRange(1, 2, "eur").Currency);

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Name <a@b.com>")]
    [InlineData("")]
    public void Candidate_email_is_validated(string email)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Candidate("Someone", email));
        Assert.Equal("email", ex.Field);
    }

    [Fact]
    public void Candidate_email_and_skills_are_normalised()
    {
        var candidate = new Candidate("Someone", "Some.One@Example.com", skills: ["C#", "c#", " SQL ", ""]);

        Assert.Equal("some.one@example.com", candidate.Email);
        Assert.Equal(["C#", "SQL"], candidate.Skills);
    }

    [Fact]
    public void Job_lifecycle_is_draft_open_closed()
    {
        var job = new JobPosting(Guid.NewGuid(), "Engineer", "Build things", null, WorkMode.Onsite, EmploymentType.Contract);

        Assert.Throws<JobStatusException>(job.Close);
        job.Publish();
        Assert.Throws<JobStatusException>(job.Publish);
        job.Close();

        Assert.Equal(JobStatus.Closed, job.Status);
        Assert.NotNull(job.PublishedAt);
        Assert.NotNull(job.ClosedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Interview_rating_is_one_to_five(int rating)
    {
        var interview = ScheduledInterview();
        Assert.Throws<DomainValidationException>(() => interview.SubmitFeedback(rating, null));
    }

    [Fact]
    public void Interview_feedback_can_only_be_submitted_once()
    {
        var interview = ScheduledInterview();
        interview.SubmitFeedback(4, "Good");

        Assert.Throws<DomainValidationException>(() => interview.SubmitFeedback(5, "Changed my mind"));
        Assert.Equal(4, interview.Rating);
    }

    private static Interview ScheduledInterview()
    {
        var job = new JobPosting(Guid.NewGuid(), "Engineer", "Build things", null, WorkMode.Remote, EmploymentType.FullTime);
        job.Publish();
        var application = JobApplication.Submit(job, new Candidate("Someone", "someone@example.com"));
        return Interview.Schedule(application, InterviewKind.PhoneScreen, DateTime.UtcNow, 30, "Interviewer");
    }
}
