using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.CreateItem;

/// <summary>A run store that refuses <see cref="SetItem"/> while <see cref="Fail"/> is set, counts the calls and passes everything else to <paramref name="inner"/>.</summary>
/// <param name="inner">The store behind it.</param>
internal sealed class SetItemFailingRunStore(IWorkflowRunStore inner) : IWorkflowRunStore
{
    /// <summary>Whether SetItem refuses.</summary>
    public bool Fail { get; set; }

    /// <summary>The run whose node runs can never be read, or null.</summary>
    public WorkflowRunId? BrokenRun { get; set; }

    /// <summary>How many SetItem calls it got, refused or not.</summary>
    public int SetItemCalls { get; private set; }

    public ValueTask<Result<WorkflowRun>> SetItem(WorkflowRunId id, ItemId item, CancellationToken cancellationToken)
    {
        SetItemCalls++;
        return Fail ? ValueTask.FromResult<Result<WorkflowRun>>(SindriErrors.LedgerUnavailable.With("runs.db: database or disk is full")) : inner.SetItem(id, item, cancellationToken);
    }

    public ValueTask<Result<WorkflowRun>> Insert(WorkflowRun run, CancellationToken cancellationToken) => inner.Insert(run, cancellationToken);

    public ValueTask<Result<GraphStartKind>> Admit(WorkflowRun run, GraphConcurrency mode, CancellationToken cancellationToken) => inner.Admit(run, mode, cancellationToken);

    public ValueTask<Result<WorkflowRun?>> PromoteOldestQueued(WorkflowId workflow, CancellationToken cancellationToken) => inner.PromoteOldestQueued(workflow, cancellationToken);

    public ValueTask<Result<WorkflowRun>> Get(WorkflowRunId id, CancellationToken cancellationToken) => inner.Get(id, cancellationToken);

    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> InFlight(WorkflowId workflow, CancellationToken cancellationToken) => inner.InFlight(workflow, cancellationToken);

    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Recent(WorkflowId workflow, int offset, int limit, CancellationToken cancellationToken) => inner.Recent(workflow, offset, limit, cancellationToken);

    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Running(CancellationToken cancellationToken) => inner.Running(cancellationToken);

    public ValueTask<Result<IReadOnlyList<WorkflowId>>> QueuedWorkflows(CancellationToken cancellationToken) => inner.QueuedWorkflows(cancellationToken);

    public ValueTask<Result<int?>> LatestOccurrence(WorkflowId workflow, string triggerKind, CancellationToken cancellationToken) => inner.LatestOccurrence(workflow, triggerKind, cancellationToken);

    public ValueTask<Result<Unit>> MarkTick(WorkflowId workflow, DateTimeOffset tick, CancellationToken cancellationToken) => inner.MarkTick(workflow, tick, cancellationToken);

    public ValueTask<Result<DateTimeOffset?>> LastTick(WorkflowId workflow, CancellationToken cancellationToken) => inner.LastTick(workflow, cancellationToken);

    public ValueTask<Result<WorkflowRun>> SetStatus(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, CancellationToken cancellationToken) => inner.SetStatus(id, status, outputs, error, cancellationToken);

    public ValueTask<Result<WorkflowRun>> CancelQueued(WorkflowRunId id, string? error, CancellationToken cancellationToken) => inner.CancelQueued(id, error, cancellationToken);

    public ValueTask<Result<bool>> Discard(WorkflowRunId id, CancellationToken cancellationToken) => inner.Discard(id, cancellationToken);

    public ValueTask<Result<NodeRun>> RecordNodeRun(NodeRun nodeRun, CancellationToken cancellationToken) => inner.RecordNodeRun(nodeRun, cancellationToken);

    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRuns(WorkflowRunId id, CancellationToken cancellationToken) =>
        BrokenRun == id ? ValueTask.FromResult<Result<IReadOnlyList<NodeRun>>>(SindriErrors.LedgerUnavailable.With("runs.db: cannot read")) : inner.NodeRuns(id, cancellationToken);

    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRunsForItem(WorkflowRunId id, int itemIndex, CancellationToken cancellationToken) => inner.NodeRunsForItem(id, itemIndex, cancellationToken);
}
