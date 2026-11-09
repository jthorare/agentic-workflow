using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Stops the Temporal workflow of a run (a port so the cancel slice is testable without a Temporal server).</summary>
public interface IGraphRunCanceller
{
    /// <summary>Asks the run's workflow to cancel; the workflow ends the run as 'cancelled'.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Success, or why the cancel was not delivered.</returns>
    public ValueTask<Result<Unit>> Cancel(WorkflowRunId run, CancellationToken cancellationToken);
}
