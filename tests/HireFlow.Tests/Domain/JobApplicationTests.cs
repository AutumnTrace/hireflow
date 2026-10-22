using HireFlow.Domain.Applications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.Interviews;
using HireFlow.Domain.Jobs;

namespace HireFlow.Tests.Domain;

public sealed class JobApplicationTests
{
    private static JobPosting OpenJob()
    {
        var job = new JobPosting(Guid.NewGuid(), "Engineer", "Build things", null, WorkMode.Remote, EmploymentType.FullTime);
        job.Publish();
        return job;
    }

    private static Candidate SomeCandidate() => new("Test Person", "test@example.com");

    [Fact]
    public void Submit_starts_in_applied_and_records_history()
    {
        var application = JobApplication.Submit(OpenJob(), SomeCandidate());

        Assert.Equal(ApplicationStage.Applied, application.Stage);
        var entry = Assert.Single(application.History);
        Assert.Null(entry.From);
        Assert.Equal(ApplicationStage.Applied, entry.To);
    }

    [Theory]
    [InlineData(JobStatus.Draft)]
    [InlineData(JobStatus.Closed)]
    public void Submit_requires_an_open_posting(JobStatus status)
    {
        var job = new JobPosting(Guid.NewGuid(), "Engineer", "Build things", null, WorkMode.Remote, EmploymentType.FullTime);
        if (status == JobStatus.Closed)
        {
            job.Publish();
            job.Close();
        }

        var ex = Assert.Throws<JobStatusException>(() => JobApplication.Submit(job, SomeCandidate()));
        Assert.Equal(status, ex.Status);
    }

    [Fact]
    public void Happy_path_walks_the_whole_pipeline()
    {
        var application = JobApplication.Submit(OpenJob(), SomeCandidate());

        application.MoveTo(ApplicationStage.Screening);
        application.MoveTo(ApplicationStage.Interview);
        application.MoveTo(ApplicationStage.Offer, "70k");
        application.MoveTo(ApplicationStage.Hired);

        Assert.Equal(ApplicationStage.Hired, application.Stage);
        Assert.False(application.IsActive);
        Assert.Equal(
            [ApplicationStage.Applied, ApplicationStage.Screening, ApplicationStage.Interview, ApplicationStage.Offer, ApplicationStage.Hired],
            application.History.Select(h => h.To));
    }

    [Theory]
    [InlineData(ApplicationStage.Interview)]
    [InlineData(ApplicationStage.Offer)]
    [InlineData(ApplicationStage.Hired)]
    [InlineData(ApplicationStage.Applied)]
    public void Cannot_skip_stages(ApplicationStage target)
    {
        var application = JobApplication.Submit(OpenJob(), SomeCandidate());

        var ex = Assert.Throws<InvalidStageTransitionException>(() => application.MoveTo(target));

        Assert.Equal(ApplicationStage.Applied, ex.From);
        Assert.Equal(target, ex.To);
        Assert.Equal(ApplicationStage.Applied, application.Stage);
    }

    [Theory]
    [InlineData(ApplicationStage.Rejected)]
    [InlineData(ApplicationStage.Withdrawn)]
    public void Can_leave_the_pipeline_from_any_active_stage(ApplicationStage exit)
    {
        foreach (var stage in new[] { ApplicationStage.Applied, ApplicationStage.Screening, ApplicationStage.Interview, ApplicationStage.Offer })
        {
            Assert.True(ApplicationStageRules.CanTransition(stage, exit), $"{stage} -> {exit} should be allowed");
        }
    }

    [Theory]
    [InlineData(ApplicationStage.Hired)]
    [InlineData(ApplicationStage.Rejected)]
    [InlineData(ApplicationStage.Withdrawn)]
    public void Terminal_stages_have_no_way_out(ApplicationStage terminal)
    {
        Assert.True(ApplicationStageRules.IsTerminal(terminal));
        Assert.Empty(ApplicationStageRules.AllowedNext(terminal));
    }

    [Fact]
    public void Cannot_schedule_interviews_for_closed_applications()
    {
        var application = JobApplication.Submit(OpenJob(), SomeCandidate());
        application.MoveTo(ApplicationStage.Rejected);

        Assert.Throws<ApplicationClosedException>(
            () => Interview.Schedule(application, InterviewKind.Technical, DateTime.UtcNow.AddDays(1), 60, "Interviewer"));
    }
}
