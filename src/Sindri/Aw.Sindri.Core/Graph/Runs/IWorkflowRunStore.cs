using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Where runs and node runs live (ledger.db, issue 2258, PR 6).</summary>
public interface IWorkflowRunStore
{
    /// <summary>Records a new run.</summary>
    /// <param name="run">The run; its id is the primary key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored run, or SND-GRAPH-100 when the id is used (a re-delivered trigger or a catch-up overlap).</returns>
    public ValueTask<Result<WorkflowRun>> Insert(WorkflowRun run, CancellationToken cancellationToken);

    /// <summary>
    /// Decides and records a trigger's run in one write: refuses a used id, then by <paramref name="mode"/> inserts the run as running (nothing else in flight, or 'unlimited'),
    /// inserts it as queued ('queue' with a run in flight) or inserts nothing ('one-per-workflow' with a run in flight).
    /// </summary>
    /// <param name="run">The run, with status running; its id is the primary key.</param>
    /// <param name="mode">The workflow's concurrency mode.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What happened, or SND-GRAPH-100 when the id is used.</returns>
    public ValueTask<Result<GraphStartKind>> Admit(WorkflowRun run, GraphConcurrency mode, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the next queued run in one write: when nothing of <paramref name="workflow"/> is running or waiting, the oldest queued run becomes running and is returned.
    /// The check and the change are one step, so two runs ending together cannot both promote one.
    /// </summary>
    /// <param name="workflow">The workflow whose run just ended.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The run, now running; null when none is queued or another run is running or waiting.</returns>
    public ValueTask<Result<WorkflowRun?>> PromoteOldestQueued(WorkflowId workflow, CancellationToken cancellationToken);

    /// <summary>One run.</summary>
    /// <param name="id">The run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The run, or SND-GRAPH-101.</returns>
    public ValueTask<Result<WorkflowRun>> Get(WorkflowRunId id, CancellationToken cancellationToken);

    /// <summary>The runs of a workflow that are queued, running or waiting, oldest first.</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> InFlight(WorkflowId workflow, CancellationToken cancellationToken);

    /// <summary>A workflow's live runs in every status (a test run is never listed), newest first (by start time, then by occurrence number, so occurrence 10 comes before 9), one page.</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="offset">How many of the newest runs to skip, 0 or more.</param>
    /// <param name="limit">How many runs to keep, 1 or more.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs; none when the workflow has none.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Recent(WorkflowId workflow, int offset, int limit, CancellationToken cancellationToken);

    /// <summary>Every run in 'running', over all workflows, oldest first (serve start reads it to find runs whose Temporal workflow was never started).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Running(CancellationToken cancellationToken);

    /// <summary>The workflows that have at least one run in 'queued' (the sweep starts the head of each queue whose workflow has nothing in flight, issue 3102).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each workflow once; none when nothing is queued.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowId>>> QueuedWorkflows(CancellationToken cancellationToken);

    /// <summary>The highest occurrence among a workflow's runs started by one kind of trigger, over every revision and every status (a schedule reads its last fired tick from it).</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="triggerKind">The trigger's 'kind', such as 'schedule'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The occurrence, or null when no such run exists.</returns>
    public ValueTask<Result<int?>> LatestOccurrence(WorkflowId workflow, string triggerKind, CancellationToken cancellationToken);

    /// <summary>Records that a schedule tick was handled (fired, queued or skipped), so only later ticks fire. It only moves forward: an earlier tick than the one on record changes nothing.</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="tick">The tick, in UTC.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the reason the write failed.</returns>
    public ValueTask<Result<Unit>> MarkTick(WorkflowId workflow, DateTimeOffset tick, CancellationToken cancellationToken);

    /// <summary>The latest schedule tick recorded by <see cref="MarkTick"/>.</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tick, or null when none was recorded.</returns>
    public ValueTask<Result<DateTimeOffset?>> LastTick(WorkflowId workflow, CancellationToken cancellationToken);

    /// <summary>
    /// Moves a run that has not ended to a new status; a final status also sets its end time. The change is conditional and the answer is the row as it stands afterwards:
    /// a run that already ended is not touched (the caller sees a status other than the one it asked for and writes no event), and a move to 'queued' applies only to a running run.
    /// </summary>
    /// <param name="id">The run.</param>
    /// <param name="status">The new status.</param>
    /// <param name="outputs">The run's outputs as JSON, or null to keep what is there.</param>
    /// <param name="error">Why it failed, or null.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The run after the call (changed or not), or SND-GRAPH-101.</returns>
    public ValueTask<Result<WorkflowRun>> SetStatus(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, CancellationToken cancellationToken);

    /// <summary>Links a run to the item its work made (a 'create_item' node). The first link wins: a run that already has an item keeps it (a 'create_item' in a loop links the first item made). It applies to a run in any status.</summary>
    /// <param name="id">The run.</param>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The run after the call, or SND-GRAPH-101.</returns>
    public ValueTask<Result<WorkflowRun>> SetItem(WorkflowRunId id, ItemId item, CancellationToken cancellationToken);

    /// <summary>Cancels a run only while it is still queued. A run that was promoted to running meanwhile is left alone.</summary>
    /// <param name="id">The run.</param>
    /// <param name="error">Why, or null.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The run after the call: 'cancelled' when this call did it, any other status when it did not; or SND-GRAPH-101.</returns>
    public ValueTask<Result<WorkflowRun>> CancelQueued(WorkflowRunId id, string? error, CancellationToken cancellationToken);

    /// <summary>Removes a run that was recorded as running but never reached Temporal (the server was unreachable), so the same run id can be tried again. A run in any other status is left alone.</summary>
    /// <param name="id">The run.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when the row was removed; false when it was not running or does not exist.</returns>
    public ValueTask<Result<bool>> Discard(WorkflowRunId id, CancellationToken cancellationToken);

    /// <summary>Writes one node run, replacing the row with the same run, node and item index. Input and output over <see cref="NodeRunLimits.PayloadBytes"/> are cut and 'Truncated' is set.</summary>
    /// <param name="nodeRun">The node run.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The node run as stored, or SND-GRAPH-101 when the run does not exist.</returns>
    public ValueTask<Result<NodeRun>> RecordNodeRun(NodeRun nodeRun, CancellationToken cancellationToken);

    /// <summary>A run's node runs, by node id then item index.</summary>
    /// <param name="id">The run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The node runs.</returns>
    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRuns(WorkflowRunId id, CancellationToken cancellationToken);

    /// <summary>The rows a node inside a 'for_each' reads: every row at item index 0 (the trigger side, the 'for_each' and the nodes outside the loop) and the rows of one item. Not the other items' rows, so a loop of N items does not read N squared rows.</summary>
    /// <param name="id">The run.</param>
    /// <param name="itemIndex">The item's position counted from 1.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The node runs, by node id then item index.</returns>
    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRunsForItem(WorkflowRunId id, int itemIndex, CancellationToken cancellationToken);
}
