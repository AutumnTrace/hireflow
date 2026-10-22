using HireFlow.Domain.Applications;
using HotChocolate.Authorization;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;

namespace HireFlow.Api.GraphQL.Applications;

public static class Topics
{
    public static string StageChanged(Guid jobId) => $"job:{jobId}:stage-changed";

    public static string ApplicationSubmitted(Guid jobId) => $"job:{jobId}:application-submitted";
}

/// <summary>Event published whenever an application moves through the pipeline.</summary>
public sealed record ApplicationStageChanged(
    Guid ApplicationId,
    Guid JobPostingId,
    ApplicationStage? From,
    ApplicationStage To,
    string? Note,
    DateTime ChangedAt);

[ExtendObjectType<ApplicationStageChanged>]
public static class ApplicationStageChangedNode
{
    public static async Task<JobApplication> GetApplicationAsync(
        [Parent] ApplicationStageChanged change, IApplicationByIdDataLoader loader, CancellationToken ct)
        => await loader.LoadRequiredAsync(change.ApplicationId, ct);
}

[SubscriptionType]
public static class ApplicationSubscriptions
{
    /// <summary>Live pipeline board: fires every time an application for the given job changes stage.</summary>
    [Authorize]
    [Subscribe(With = nameof(SubscribeToStageChangesAsync))]
    public static ApplicationStageChanged OnApplicationStageChanged(
        Guid jobPostingId, [EventMessage] ApplicationStageChanged change)
        => change;

    /// <summary>Fires when a new candidate applies to the given job.</summary>
    [Authorize]
    [Subscribe(With = nameof(SubscribeToSubmissionsAsync))]
    public static JobApplication OnApplicationSubmitted(
        Guid jobPostingId, [EventMessage] JobApplication application)
        => application;

    public static ValueTask<ISourceStream<ApplicationStageChanged>> SubscribeToStageChangesAsync(
        Guid jobPostingId, ITopicEventReceiver receiver, CancellationToken ct)
        => receiver.SubscribeAsync<ApplicationStageChanged>(Topics.StageChanged(jobPostingId), ct);

    public static ValueTask<ISourceStream<JobApplication>> SubscribeToSubmissionsAsync(
        Guid jobPostingId, ITopicEventReceiver receiver, CancellationToken ct)
        => receiver.SubscribeAsync<JobApplication>(Topics.ApplicationSubmitted(jobPostingId), ct);
}
