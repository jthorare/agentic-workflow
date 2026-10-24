using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Google.Protobuf;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;
using ApiExecution = Temporalio.Api.Common.V1.WorkflowExecution;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What one running recipe instance waits on, read from Temporal (API spec §3.8, E-30): its pending activity or child from the execution's description, and its running timers and the
/// time it last settled from the newest events of its history. An instance with no pending activity, child or timer waits for a signal (an update arrives through the same wait), since the
/// time its last workflow task completed.
/// </summary>
internal static class EngineInstanceScan
{
    /// <summary>How many events one history page holds.</summary>
    private const int PageSize = 100;

    /// <summary>
    /// How many pages back from the newest event are read to find the open timers: 1000 pages, 100000 events. Describe doesn't list timers, so a timer started before
    /// the bound and still open is missed, and an instance whose open timer is that old reads as a signal wait. A history that short of the bound is read whole.
    /// </summary>
    private const int MaxPages = 1000;

    /// <summary>The facts of the instance <paramref name="execution"/> lists; null when it isn't a Sindri recipe instance (no recipe id, or an address that isn't one).</summary>
    /// <param name="client">The client.</param>
    /// <param name="settings">The namespace.</param>
    /// <param name="execution">The visibility row.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or null.</returns>
    internal static async Task<EngineInstanceFacts?> Read(ITemporalClient client, TemporalSettings settings, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(execution);
        if (Identify(execution) is not { } who)
        {
            return null;
        }

        var request = new DescribeWorkflowExecutionRequest { Namespace = settings.Namespace, Execution = new ApiExecution { WorkflowId = execution.Id } };
        var described = await TemporalReads.Run(rpc => client.WorkflowService.DescribeWorkflowExecutionAsync(request, rpc), cancellationToken, TemporalEngineReader.Deadline).ConfigureAwait(false);
        var events = await Newest(client, settings, execution.Id, cancellationToken).ConfigureAwait(false);
        return Facts(who, described, events, new DateTimeOffset(DateTime.SpecifyKind(execution.StartTime, DateTimeKind.Utc)));
    }

    private static Identity? Identify(WorkflowExecution execution)
    {
        var attributes = execution.TypedSearchAttributes;
        if (!InstanceAddress.TryFrom(execution.Id, out var address) || !attributes.TryGetValue(SindriTemporal.RecipeKey, out var recipe) || !RecipeId.TryFrom(recipe, out var recipeId))
        {
            return null;
        }

        return new Identity(address, recipeId, attributes.TryGetValue(SindriTemporal.RecipeHashKey, out var hash) ? hash : null, attributes.TryGetValue(SindriTemporal.StateKey, out var state) ? state : null);
    }

    private static EngineInstanceFacts Facts(Identity who, DescribeWorkflowExecutionResponse described, List<HistoryEvent> events, DateTimeOffset started)
    {
        var timers = OpenTimers(events);
        var settled = events.FirstOrDefault(static e => e.EventType == EventType.WorkflowTaskCompleted) ?? events.FirstOrDefault();
        var since = settled?.EventTime.ToDateTimeOffset() ?? started;
        var pending = described.PendingActivities.FirstOrDefault();
        return new EngineInstanceFacts(
            who.Address,
            new EngineRecipeRef(who.Recipe, who.Hash),
            who.State,
            Waiting(described, pending, timers, since),
            timers,
            pending is { Attempt: > 1 } retrying ? new EngineRetries(retrying.Attempt, retrying.LastFailure?.Message) : null,
            described.WorkflowExecutionInfo.HistoryLength);
    }

    private sealed record Identity(InstanceAddress Address, RecipeId Recipe, string? Hash, string? State);

    /// <summary>What an instance waits on: its pending activity, else a pending child, else an open timer, else a signal.</summary>
    /// <param name="description">The execution's description.</param>
    /// <param name="activity">Its first pending activity, if any.</param>
    /// <param name="timers">Its open timers.</param>
    /// <param name="since">When it last settled.</param>
    /// <returns>The wait.</returns>
    internal static EngineWaiting Waiting(DescribeWorkflowExecutionResponse description, Temporalio.Api.Workflow.V1.PendingActivityInfo? activity, List<EngineTimer> timers, DateTimeOffset since) =>
        activity is not null
            ? new EngineWaiting(EngineWaitKinds.Activity, activity.ActivityType.Name, (activity.LastStartedTime ?? activity.ScheduledTime)?.ToDateTimeOffset() ?? since)
            : description.PendingChildren.Count > 0
                ? new EngineWaiting(EngineWaitKinds.Child, Name: null, since)
                : timers.Count > 0
                    ? new EngineWaiting(EngineWaitKinds.Timer, timers[0].Name, since)
                    : new EngineWaiting(EngineWaitKinds.Signal, Name: null, since);

    /// <summary>The timers started and neither fired nor cancelled in <paramref name="newestFirst"/>.</summary>
    /// <param name="newestFirst">The newest events, newest first.</param>
    /// <returns>The open timers, oldest first.</returns>
    internal static List<EngineTimer> OpenTimers(IReadOnlyList<HistoryEvent> newestFirst)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        var closed = new HashSet<string>(StringComparer.Ordinal);
        var open = new List<EngineTimer>();
        foreach (var e in newestFirst)
        {
            if (e.EventType == EventType.TimerFired)
            {
                _ = closed.Add(e.TimerFiredEventAttributes.TimerId);
            }
            else if (e.EventType == EventType.TimerCanceled)
            {
                _ = closed.Add(e.TimerCanceledEventAttributes.TimerId);
            }
            else if (e.EventType == EventType.TimerStarted && !closed.Contains(e.TimerStartedEventAttributes.TimerId))
            {
                var started = e.TimerStartedEventAttributes;
                open.Add(new EngineTimer($"timer-{started.TimerId}", e.EventTime.ToDateTimeOffset() + started.StartToFireTimeout.ToTimeSpan(), "timeout"));
            }
        }

        open.Reverse();
        return open;
    }

    /// <summary>The newest events, newest first: page after page back through the history until its start, or <see cref="MaxPages"/> pages.</summary>
    private static async Task<List<HistoryEvent>> Newest(ITemporalClient client, TemporalSettings settings, string workflowId, CancellationToken cancellationToken)
    {
        var events = new List<HistoryEvent>();
        var continuation = ByteString.Empty;
        for (var page = 0; page < MaxPages; page++)
        {
            var request = new GetWorkflowExecutionHistoryReverseRequest { Namespace = settings.Namespace, Execution = new ApiExecution { WorkflowId = workflowId }, MaximumPageSize = PageSize, NextPageToken = continuation };
            var response = await TemporalReads.Run(rpc => client.WorkflowService.GetWorkflowExecutionHistoryReverseAsync(request, rpc), cancellationToken, TemporalEngineReader.Deadline).ConfigureAwait(false);
            events.AddRange(response.History.Events);
            continuation = response.NextPageToken;
            if (continuation.IsEmpty)
            {
                break;
            }
        }

        return events;
    }
}
