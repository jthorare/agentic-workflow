using Aw.Kernel;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>'workflow' and 'workflow_revision' in ledger.db (issue 2258, Data): the source of truth for workflow graphs, so they sit beside the ledger and not in views.db.</summary>
internal sealed class SqliteWorkflowStore : IWorkflowStore
{
    /// <summary>The store over the ledger file.</summary>
    /// <param name="database">The ledger file.</param>
    /// <param name="clock">The clock the revisions' times come from.</param>
    public SqliteWorkflowStore(LedgerDatabase database, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);
    }

    public ValueTask<Result<WorkflowRevision>> Create(WorkflowId id, WorkflowSlug slug, GraphSpec spec, string createdBy, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<WorkflowRevision>> Save(WorkflowId id, int baseRevision, GraphSpec spec, string createdBy, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<WorkflowRecord>> Get(WorkflowId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<WorkflowRevision>> GetRevision(WorkflowId id, int? revision, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<WorkflowRecord>>> List(bool includeArchived, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<WorkflowRecord>> Activate(WorkflowId id, int revision, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<WorkflowRecord>> Archive(WorkflowId id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
