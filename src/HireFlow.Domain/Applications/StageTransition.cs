namespace HireFlow.Domain.Applications;

/// <summary>Immutable audit record of one pipeline move.</summary>
public sealed class StageTransition
{
    private StageTransition()
    {
    }

    internal StageTransition(Guid applicationId, ApplicationStage? from, ApplicationStage to, string? note, DateTime changedAt)
    {
        ApplicationId = applicationId;
        From = from;
        To = to;
        Note = note;
        ChangedAt = changedAt;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid ApplicationId { get; private set; }

    public ApplicationStage? From { get; private set; }

    public ApplicationStage To { get; private set; }

    public string? Note { get; private set; }

    public DateTime ChangedAt { get; private set; }
}
