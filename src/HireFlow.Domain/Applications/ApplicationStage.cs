namespace HireFlow.Domain.Applications;

public enum ApplicationStage
{
    Applied,
    Screening,
    Interview,
    Offer,
    Hired,
    Rejected,
    Withdrawn,
}

/// <summary>
/// The hiring pipeline as an explicit state machine. Keeping the transition table in one
/// place means the API, the UI hints (<see cref="AllowedNext"/>) and the tests all agree.
/// </summary>
public static class ApplicationStageRules
{
    private static readonly Dictionary<ApplicationStage, ApplicationStage[]> Transitions = new()
    {
        [ApplicationStage.Applied] = [ApplicationStage.Screening, ApplicationStage.Rejected, ApplicationStage.Withdrawn],
        [ApplicationStage.Screening] = [ApplicationStage.Interview, ApplicationStage.Rejected, ApplicationStage.Withdrawn],
        [ApplicationStage.Interview] = [ApplicationStage.Offer, ApplicationStage.Rejected, ApplicationStage.Withdrawn],
        [ApplicationStage.Offer] = [ApplicationStage.Hired, ApplicationStage.Rejected, ApplicationStage.Withdrawn],
        [ApplicationStage.Hired] = [],
        [ApplicationStage.Rejected] = [],
        [ApplicationStage.Withdrawn] = [],
    };

    public static IReadOnlyList<ApplicationStage> AllowedNext(ApplicationStage from) => Transitions[from];

    public static bool CanTransition(ApplicationStage from, ApplicationStage to) => Transitions[from].Contains(to);

    public static bool IsTerminal(ApplicationStage stage) => Transitions[stage].Length == 0;
}
