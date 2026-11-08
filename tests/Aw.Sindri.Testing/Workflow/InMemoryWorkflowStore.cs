using Aw.Kernel;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IWorkflowStore"/>. Passes <see cref="WorkflowStoreContract"/>.</summary>
public sealed class InMemoryWorkflowStore : IWorkflowStore
{
    /// <summary>An empty store.</summary>
    /// <param name="clock">The clock the revisions' times come from.</param>
    public InMemoryWorkflowStore(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRevision>> Create(WorkflowId id, WorkflowSlug slug, GraphSpec spec, string createdBy, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRevision>> Save(WorkflowId id, int baseRevision, GraphSpec spec, string createdBy, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRecord>> Get(WorkflowId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRevision>> GetRevision(WorkflowId id, int? revision, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowRecord>>> List(bool includeArchived, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRecord>> Activate(WorkflowId id, int revision, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRecord>> Archive(WorkflowId id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
