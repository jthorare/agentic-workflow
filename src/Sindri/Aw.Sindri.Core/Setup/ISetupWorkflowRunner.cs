using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Setup;

/// <summary>Starts a run of a workflow as the setup service does (the RunWorkflow command, so the same checks and the same run record as a person's `sindri wf run`).</summary>
public interface ISetupWorkflowRunner
{
    /// <summary>Starts one run of the active revision.</summary>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>Unit when the run started, or why the command refused.</returns>
    public ValueTask<Result<Unit>> Run(WorkflowSlug slug, CancellationToken cancellationToken);
}
