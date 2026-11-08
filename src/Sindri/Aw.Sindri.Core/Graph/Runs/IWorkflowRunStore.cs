using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

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

    /// <summary>The highest occurrence among a workflow's runs started by one kind of trigger, over every revision and every status (a schedule reads its last fired tick from it).</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="triggerKind">The trigger's 'kind', such as 'schedule'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The occurrence, or null when no such run exists.</returns>
    public ValueTask<Result<int?>> LatestOccurrence(WorkflowId workflow, string triggerKind, CancellationToken cancellationToken);

    /// <summary>Moves a run to a new status; a final status also sets its end time.</summary>
    /// <param name="id">The run.</param>
    /// <param name="status">The new status.</param>
    /// <param name="outputs">The run's outputs as JSON, or null to keep what is there.</param>
    /// <param name="error">Why it failed, or null.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The run after the change, or SND-GRAPH-101.</returns>
    public ValueTask<Result<WorkflowRun>> SetStatus(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, CancellationToken cancellationToken);

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
}
