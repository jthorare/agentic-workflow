using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph;

/// <summary>
/// Where workflows and their revisions live (ledger.db; the source of truth, not rebuildable). Every save is a new
/// revision and never changes an old one; activating pins the revision triggers run.
/// </summary>
public interface IWorkflowStore
{
    /// <summary>Creates a workflow with its first revision.</summary>
    /// <param name="id">The new id.</param>
    /// <param name="slug">Its slug; one already taken is SND-GRAPH-009.</param>
    /// <param name="spec">The first graph.</param>
    /// <param name="createdBy">'owner' or 'session:&lt;id&gt;'.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Revision 1.</returns>
    public ValueTask<Result<WorkflowRevision>> Create(WorkflowId id, WorkflowSlug slug, GraphSpec spec, string createdBy, CancellationToken cancellationToken);

    /// <summary>Saves <paramref name="spec"/> as the next revision of a workflow.</summary>
    /// <param name="id">The workflow.</param>
    /// <param name="baseRevision">The revision the author edited; if it is no longer the latest the save is SND-GRAPH-007.</param>
    /// <param name="spec">The edited graph.</param>
    /// <param name="createdBy">'owner' or 'session:&lt;id&gt;'.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new revision.</returns>
    public ValueTask<Result<WorkflowRevision>> Save(WorkflowId id, int baseRevision, GraphSpec spec, string createdBy, CancellationToken cancellationToken);

    /// <summary>A workflow's record.</summary>
    /// <param name="id">The workflow.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record, or SND-GRAPH-008.</returns>
    public ValueTask<Result<WorkflowRecord>> Get(WorkflowId id, CancellationToken cancellationToken);

    /// <summary>One revision of a workflow.</summary>
    /// <param name="id">The workflow.</param>
    /// <param name="revision">The revision, or null for the latest.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The revision, or SND-GRAPH-008.</returns>
    public ValueTask<Result<WorkflowRevision>> GetRevision(WorkflowId id, int? revision, CancellationToken cancellationToken);

    /// <summary>The workflows, by slug.</summary>
    /// <param name="includeArchived">True to list archived ones too.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The records.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowRecord>>> List(bool includeArchived, CancellationToken cancellationToken);

    /// <summary>Pins the revision triggers run.</summary>
    /// <param name="id">The workflow.</param>
    /// <param name="revision">An existing revision.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The record after the change, or SND-GRAPH-008.</returns>
    public ValueTask<Result<WorkflowRecord>> Activate(WorkflowId id, int revision, CancellationToken cancellationToken);

    /// <summary>Archives a workflow: it leaves the list and turns off.</summary>
    /// <param name="id">The workflow.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The record after the change, or SND-GRAPH-008.</returns>
    public ValueTask<Result<WorkflowRecord>> Archive(WorkflowId id, CancellationToken cancellationToken);
}
