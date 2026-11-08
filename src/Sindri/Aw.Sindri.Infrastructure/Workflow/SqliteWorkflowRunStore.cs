using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>'workflow_run' and 'workflow_node_run' in ledger.db (issue 2258, PR 6): the run id is the primary key, so a second insert of one id is refused.</summary>
/// <param name="database">The ledger file.</param>
/// <param name="clock">The clock the end times come from.</param>
internal sealed class SqliteWorkflowRunStore(LedgerDatabase database, TimeProvider clock) : IWorkflowRunStore
{
    private readonly LedgerDatabase _database = database;
    private readonly TimeProvider _clock = clock;

    public ValueTask<Result<WorkflowRun>> Insert(WorkflowRun run, CancellationToken cancellationToken)
    {
        _ = (_database, _clock);
        throw new NotSupportedException();
    }

    public ValueTask<Result<WorkflowRun>> Get(WorkflowRunId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> InFlight(WorkflowId workflow, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<WorkflowRun>> SetStatus(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<NodeRun>> RecordNodeRun(NodeRun nodeRun, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRuns(WorkflowRunId id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
