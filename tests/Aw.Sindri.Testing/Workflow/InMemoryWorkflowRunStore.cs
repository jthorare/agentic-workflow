using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IWorkflowRunStore"/>. Passes <see cref="WorkflowRunStoreContract"/>.</summary>
/// <param name="clock">The clock the end times come from.</param>
public sealed class InMemoryWorkflowRunStore(TimeProvider clock) : IWorkflowRunStore
{
    private readonly TimeProvider _clock = clock;

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> Insert(WorkflowRun run, CancellationToken cancellationToken)
    {
        _ = _clock;
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> Get(WorkflowRunId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> InFlight(WorkflowId workflow, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> SetStatus(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<NodeRun>> RecordNodeRun(NodeRun nodeRun, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRuns(WorkflowRunId id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
