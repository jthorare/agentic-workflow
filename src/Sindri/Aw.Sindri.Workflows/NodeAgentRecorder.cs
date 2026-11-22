using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Launch;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Writes the 'workflow.node.agent.changed' events of one 'run_agent' node run (#3151, Agents page E1), one per change. The series count lives here, in the workflow, so a replay
/// writes the same keys and the ledger keeps each once. Workflow code only calls an activity; the write is the activity's.
/// </summary>
/// <param name="run">The run.</param>
/// <param name="node">The node.</param>
/// <param name="itemIndex">The item's position, 0 for a node that runs once.</param>
/// <param name="enabled">False for a history recorded before the events, which has no activity calls for them.</param>
internal sealed class NodeAgentRecorder(WorkflowRunId run, NodeId node, int itemIndex, bool enabled)
{
    private static readonly ActivityOptions _write = new() { StartToCloseTimeout = TimeSpan.FromMinutes(5), RetryPolicy = GraphActivityOptions.Unbounded };
    private static readonly ActivityOptions _detached = GraphActivityOptions.Detached;

    /// <summary>True when the workflow watches the session's permission prompt; false for a history that predates it.</summary>
    public bool Asks { get; set; }

    private int _sequence;
    private string? _last;
    private string? _state;
    private string? _waitingOn;
    private LaunchId? _launch;
    private string? _branch;
    private DateTimeOffset? _deadline;
    private string? _tool;
    private DateTimeOffset? _toolAt;

    /// <summary>Remembers the launch and its branch, which every event from here on carries.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="branch">The branch the session may push.</param>
    public void Launched(LaunchId launch, string branch)
    {
        _launch = launch;
        _branch = branch;
    }

    /// <summary>Remembers when the node's timeout ends it.</summary>
    /// <param name="deadline">Launch time plus the timeout.</param>
    public void DeadlineAt(DateTimeOffset deadline) => _deadline = deadline;

    /// <summary>Moves the deadline (a handback restarts the clock with what was left) and records the current state again, which carries the new deadline.</summary>
    /// <param name="deadline">Now plus the time left.</param>
    /// <returns>Completes when the event is written.</returns>
    public Task MoveDeadline(DateTimeOffset deadline)
    {
        _deadline = deadline;
        return _state is { } state ? Write(state, _waitingOn, ask: null, _write) : Task.CompletedTask;
    }

    /// <summary>Records the end of a node whose run was cancelled. The write is detached, so the cancel does not cancel it.</summary>
    /// <returns>Completes when the event is written.</returns>
    public Task RecordCancelled() => Write(NodeAgentStates.EndedFailed, waitingOn: null, ask: null, _detached);

    /// <summary>Records the node's state, unless it and its waiting-on text are what the last event said.</summary>
    /// <param name="state">One of <see cref="NodeAgentStates"/>.</param>
    /// <param name="waitingOn">What the node waits on, or null.</param>
    /// <param name="ask">The permission prompt the session waits on; its tool and time are kept for the events after it.</param>
    /// <returns>Completes when the event is written.</returns>
    public Task Record(string state, string? waitingOn = null, NodeSessionAsk? ask = null) => Write(state, waitingOn, ask, _write);

    private async Task Write(string state, string? waitingOn, NodeSessionAsk? ask, ActivityOptions options)
    {
        if (ask is { Tool: { } tool })
        {
            _tool = tool;
            _toolAt = ask.At;
        }

        var shape = string.Concat(state, "|", waitingOn, "|", _tool, "|", _toolAt?.ToString("O", CultureInfo.InvariantCulture), "|", _deadline?.ToString("O", CultureInfo.InvariantCulture));
        if (!enabled || string.Equals(shape, _last, StringComparison.Ordinal))
        {
            return;
        }

        _last = shape;
        _state = state;
        _waitingOn = waitingOn;
        _sequence++;
        var payload = new WorkflowNodeAgentChangedPayload(run, node, itemIndex, state, waitingOn, _tool, _toolAt, _launch, _branch, _deadline, GraphMirror.By);
        var record = new GraphNodeAgent(payload, _sequence);
        await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAgentAsync(record), options).ConfigureAwait(true);
    }

    /// <summary>The state a finished node ends in, from the failure text the wait gave.</summary>
    /// <param name="failure">Why the node failed ('timeout: …', 'exit: …'), or null when it succeeded.</param>
    /// <returns>The ended state.</returns>
    public static string EndState(string? failure) =>
        failure is null ? NodeAgentStates.EndedSucceeded
        : failure.StartsWith("timeout:", StringComparison.Ordinal) ? NodeAgentStates.EndedTimedOut
        : NodeAgentStates.EndedFailed;

    /// <summary>The text for a launch that waits for a free slot.</summary>
    public const string SlotWait = "launch slot (waiting)";
}
