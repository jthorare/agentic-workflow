using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Setup;

/// <summary>Makes a workflow as the setup service does (the CreateWorkflow command, so the same checks and the same 'workflow.created' event as a person's create).</summary>
public interface ISetupWorkflowCreator
{
    /// <summary>Makes the workflow, off, at revision 1.</summary>
    /// <param name="slug">The new workflow's slug.</param>
    /// <param name="spec">The graph as JSON text.</param>
    /// <param name="cancellationToken">Cancels the create.</param>
    /// <returns>Unit when it was made; SND-GRAPH-009 when the slug is taken; or why the command refused.</returns>
    public ValueTask<Result<Unit>> Create(WorkflowSlug slug, string spec, CancellationToken cancellationToken);
}
