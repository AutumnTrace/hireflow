using HireFlow.Domain.Candidates;
using HireFlow.Domain.Common;
using HireFlow.Domain.Jobs;

namespace HireFlow.Domain.Applications;

/// <summary>
/// A candidate's application to a job posting. This is the aggregate root for the
/// pipeline: every stage change goes through <see cref="MoveTo"/> and is recorded
/// in <see cref="History"/>.
/// </summary>
public sealed class JobApplication
{
    private readonly List<StageTransition> _history = [];

    private JobApplication()
    {
    }

    private JobApplication(Guid jobPostingId, Guid candidateId, string? coverLetter)
    {
        JobPostingId = jobPostingId;
        CandidateId = candidateId;
        CoverLetter = Guard.Optional(coverLetter, nameof(coverLetter), 4000);
        Stage = ApplicationStage.Applied;
        AppliedAt = UpdatedAt = DateTime.UtcNow;
        _history.Add(new StageTransition(Id, from: null, to: ApplicationStage.Applied, note: null, AppliedAt));
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid JobPostingId { get; private set; }

    public Guid CandidateId { get; private set; }

    public ApplicationStage Stage { get; private set; }

    public string? CoverLetter { get; private set; }

    public DateTime AppliedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Transitions recorded by this instance. Not loaded from storage by default.</summary>
    public IReadOnlyCollection<StageTransition> History => _history;

    public bool IsActive => !ApplicationStageRules.IsTerminal(Stage);

    public static JobApplication Submit(JobPosting job, Candidate candidate, string? coverLetter = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(candidate);

        job.EnsureAcceptingApplications();
        return new JobApplication(job.Id, candidate.Id, coverLetter);
    }

    public StageTransition MoveTo(ApplicationStage target, string? note = null)
    {
        if (!ApplicationStageRules.CanTransition(Stage, target))
        {
            throw new InvalidStageTransitionException(Id, Stage, target);
        }

        var transition = new StageTransition(Id, Stage, target, Guard.Optional(note, nameof(note), 1000), DateTime.UtcNow);
        _history.Add(transition);
        Stage = target;
        UpdatedAt = transition.ChangedAt;
        return transition;
    }

    public void EnsureActive()
    {
        if (!IsActive)
        {
            throw new ApplicationClosedException(Id, Stage);
        }
    }
}

public sealed class InvalidStageTransitionException(Guid applicationId, ApplicationStage from, ApplicationStage to)
    : DomainException(BuildMessage(from, to))
{
    public Guid ApplicationId { get; } = applicationId;

    public ApplicationStage From { get; } = from;

    public ApplicationStage To { get; } = to;

    public IReadOnlyList<ApplicationStage> AllowedStages { get; } = ApplicationStageRules.AllowedNext(from);

    private static string BuildMessage(ApplicationStage from, ApplicationStage to)
    {
        var allowed = ApplicationStageRules.AllowedNext(from);
        var hint = allowed.Count == 0 ? $"{from} is a final stage." : $"Allowed: {string.Join(", ", allowed)}.";
        return $"Cannot move an application from {from} to {to}. {hint}";
    }
}

public sealed class ApplicationClosedException(Guid applicationId, ApplicationStage stage)
    : DomainException($"Application is already closed ({stage}).")
{
    public Guid ApplicationId { get; } = applicationId;

    public ApplicationStage Stage { get; } = stage;
}

public sealed class DuplicateApplicationException(Guid jobPostingId, Guid candidateId)
    : DomainException("This candidate has already applied to this job.")
{
    public Guid JobPostingId { get; } = jobPostingId;

    public Guid CandidateId { get; } = candidateId;
}
