using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Setup;

/// <summary>Turns a workflow's triggers on as the setup service does (the ActivateWorkflow command, so the same activation checks as a person's activate).</summary>
public interface ISetupWorkflowActivator
{
    /// <summary>Activates the newest revision.</summary>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the activate.</param>
    /// <returns>Unit when it is active; SND-GRAPH-011 when it was archived; or why the command refused.</returns>
    public ValueTask<Result<Unit>> Activate(WorkflowSlug slug, CancellationToken cancellationToken);
}
