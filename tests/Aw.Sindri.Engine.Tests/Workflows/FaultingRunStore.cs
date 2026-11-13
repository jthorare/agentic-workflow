using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// An <see cref="IWorkflowRunStore"/> that passes every call to the real in-memory store and answers 'Get', 'SetStatus' and 'RecordNodeRun' with a lasting error
/// (an Invalid one, not NotFound) while <see cref="FailWhen"/> says so. It stands for a store that stays broken (disk full, a locked database), issue 2999.
/// </summary>
/// <param name="inner">The store that does the work.</param>
internal sealed class FaultingRunStore(IWorkflowRunStore inner) : IWorkflowRunStore
{
    /// <summary>The text of the injected error.</summary>
    public const string Message = "injected store fault";

    /// <summary>Which calls fail; nothing fails by default.</summary>
    public Func<RunCall, bool> FailWhen { get; set; } = static _ => false;

    public ValueTask<Result<WorkflowRun>> Insert(WorkflowRun run, CancellationToken cancellationToken) => inner.Insert(run, cancellationToken);

    public ValueTask<Result<GraphStartKind>> Admit(WorkflowRun run, GraphConcurrency mode, CancellationToken cancellationToken) => inner.Admit(run, mode, cancellationToken);

    public ValueTask<Result<WorkflowRun?>> PromoteOldestQueued(WorkflowId workflow, CancellationToken cancellationToken) => inner.PromoteOldestQueued(workflow, cancellationToken);

    public ValueTask<Result<WorkflowRun>> Get(WorkflowRunId id, CancellationToken cancellationToken) =>
        FailWhen(new RunCall(RunOp.Get, id, Status: null)) ? Fault<WorkflowRun>() : inner.Get(id, cancellationToken);

    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> InFlight(WorkflowId workflow, CancellationToken cancellationToken) => inner.InFlight(workflow, cancellationToken);

    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Recent(WorkflowId workflow, int offset, int limit, CancellationToken cancellationToken) => inner.Recent(workflow, offset, limit, cancellationToken);

    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Running(CancellationToken cancellationToken) => inner.Running(cancellationToken);

    public ValueTask<Result<IReadOnlyList<WorkflowId>>> QueuedWorkflows(CancellationToken cancellationToken) => inner.QueuedWorkflows(cancellationToken);

    public ValueTask<Result<int?>> LatestOccurrence(WorkflowId workflow, string triggerKind, CancellationToken cancellationToken) => inner.LatestOccurrence(workflow, triggerKind, cancellationToken);

    public ValueTask<Result<Unit>> MarkTick(WorkflowId workflow, DateTimeOffset tick, CancellationToken cancellationToken) => inner.MarkTick(workflow, tick, cancellationToken);

    public ValueTask<Result<DateTimeOffset?>> LastTick(WorkflowId workflow, CancellationToken cancellationToken) => inner.LastTick(workflow, cancellationToken);

    public ValueTask<Result<WorkflowRun>> SetStatus(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, CancellationToken cancellationToken) =>
        FailWhen(new RunCall(RunOp.SetStatus, id, status)) ? Fault<WorkflowRun>() : inner.SetStatus(id, status, outputs, error, cancellationToken);

    public ValueTask<Result<WorkflowRun>> SetItem(WorkflowRunId id, ItemId item, CancellationToken cancellationToken) => inner.SetItem(id, item, cancellationToken);

    public ValueTask<Result<WorkflowRun>> CancelQueued(WorkflowRunId id, string? error, CancellationToken cancellationToken) => inner.CancelQueued(id, error, cancellationToken);

    public ValueTask<Result<bool>> Discard(WorkflowRunId id, CancellationToken cancellationToken) => inner.Discard(id, cancellationToken);

    public ValueTask<Result<NodeRun>> RecordNodeRun(NodeRun nodeRun, CancellationToken cancellationToken) =>
        FailWhen(new RunCall(RunOp.RecordNodeRun, nodeRun.RunId, Status: null)) ? Fault<NodeRun>() : inner.RecordNodeRun(nodeRun, cancellationToken);

    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRuns(WorkflowRunId id, CancellationToken cancellationToken) => inner.NodeRuns(id, cancellationToken);

    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRunsForItem(WorkflowRunId id, int itemIndex, CancellationToken cancellationToken) => inner.NodeRunsForItem(id, itemIndex, cancellationToken);

    private static ValueTask<Result<T>> Fault<T>() => ValueTask.FromResult<Result<T>>(SindriErrors.GraphBlockFailed.With(Message));
}
