using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Asks Temporal whether it has a workflow for a run (a port so the recovery pass is testable without a Temporal server).</summary>
public interface IGraphRunProbe
{
    /// <summary>Answers whether Temporal has a workflow whose id is the run id.</summary>
    /// <param name="run">The run id.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    /// <returns>True when Temporal knows the workflow, false when it does not, or an error when Temporal cannot be asked.</returns>
    public ValueTask<Result<bool>> Exists(WorkflowRunId run, CancellationToken cancellationToken);
}
