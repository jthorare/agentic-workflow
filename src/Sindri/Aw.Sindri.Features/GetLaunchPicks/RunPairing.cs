using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>
/// How each launch of an instance ended, from the instance's mirrored rows in order: the row that opens a run names the run's token
/// and the launch as the frame's actor; a 'step.completed' or 'step.failed' row of that token closes the oldest launch still
/// waiting on the token. A retry on a stronger model keeps the token, so its launch waits behind the one that failed.
/// </summary>
/// <param name="events">Reads each event's payload.</param>
internal sealed class RunPairing(EventFactory events)
{
    private readonly Dictionary<string, Queue<LaunchId>> _open = new(StringComparer.Ordinal);
    private readonly HashSet<LaunchId> _seen = [];

    /// <summary>The outcome word of every launch that has ended.</summary>
    public Dictionary<LaunchId, string> Ended { get; } = [];

    /// <summary>Takes one of the instance's rows in; rows must come oldest first.</summary>
    /// <param name="row">The row.</param>
    public void Add(LedgerEvent row)
    {
        var read = events.Payload<PickRunRow>(row);
        if (read.IsError)
        {
            return;
        }

        Close(read.Value);
        Open(read.Value);
    }

    private void Close(PickRunRow row)
    {
        var completed = string.Equals(row.Trigger, TriggerNames.StepCompleted, StringComparison.Ordinal);
        var failed = string.Equals(row.Trigger, TriggerNames.StepFailed, StringComparison.Ordinal);
        if ((completed || failed) && row.Run is { } token && _open.TryGetValue(token, out var waiting) && waiting.Count > 0)
        {
            Ended[waiting.Dequeue()] = completed ? LaunchPickOutcomes.Passed : FailedWord(row.Detail?.FailureKind);
        }
    }

    private void Open(PickRunRow row)
    {
        if (row.Detail?.Delta?.Frame is not { Run: { } token, Actor: { } actor } || !LaunchId.TryFrom(actor, out var launch) || !_seen.Add(launch))
        {
            return;
        }

        if (!_open.TryGetValue(token, out var queue))
        {
            _open[token] = queue = new Queue<LaunchId>();
        }

        queue.Enqueue(launch);
    }

    private static string FailedWord(string? kind) =>
        string.Equals(kind, AgentFailureKinds.Refused, StringComparison.Ordinal) ? LaunchPickOutcomes.Refused : LaunchPickOutcomes.Failed;
}
