using HireFlow.Domain.Applications;
using HireFlow.Domain.Common;

namespace HireFlow.Domain.Interviews;

public enum InterviewKind
{
    PhoneScreen,
    Technical,
    SystemDesign,
    Behavioral,
    Onsite,
}

public sealed class Interview
{
    private Interview()
    {
    }

    private Interview(Guid applicationId, InterviewKind kind, DateTime scheduledAt, int durationMinutes, string interviewer)
    {
        ApplicationId = applicationId;
        Kind = kind;
        ScheduledAt = scheduledAt;
        DurationMinutes = durationMinutes;
        Interviewer = interviewer;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid ApplicationId { get; private set; }

    public InterviewKind Kind { get; private set; }

    public DateTime ScheduledAt { get; private set; }

    public int DurationMinutes { get; private set; }

    public string Interviewer { get; private set; } = null!;

    /// <summary>1 (strong no) to 5 (strong yes). Null until feedback is submitted.</summary>
    public int? Rating { get; private set; }

    public string? Feedback { get; private set; }

    public DateTime? FeedbackSubmittedAt { get; private set; }

    public static Interview Schedule(
        JobApplication application,
        InterviewKind kind,
        DateTime scheduledAt,
        int durationMinutes,
        string interviewer)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.EnsureActive();

        if (durationMinutes is < 15 or > 480)
        {
            throw new DomainValidationException(nameof(durationMinutes), "Interviews must last between 15 minutes and 8 hours.");
        }

        return new Interview(
            application.Id,
            kind,
            DateTime.SpecifyKind(scheduledAt, DateTimeKind.Utc),
            durationMinutes,
            Guard.NotBlank(interviewer, nameof(interviewer), 120));
    }

    public void SubmitFeedback(int rating, string? feedback)
    {
        if (FeedbackSubmittedAt is not null)
        {
            throw new DomainValidationException(nameof(feedback), "Feedback has already been submitted for this interview.");
        }

        if (rating is < 1 or > 5)
        {
            throw new DomainValidationException(nameof(rating), "Rating must be between 1 and 5.");
        }

        Rating = rating;
        Feedback = Guard.Optional(feedback, nameof(feedback), 4000);
        FeedbackSubmittedAt = DateTime.UtcNow;
    }
}
