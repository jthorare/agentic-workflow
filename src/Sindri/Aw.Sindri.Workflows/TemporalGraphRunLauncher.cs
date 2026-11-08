using Aw.Kernel;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Client;

namespace Aw.Sindri.Infrastructure.Workflows;

// why: the host registers it with the starter in the green commit; the pragma goes with that registration.
#pragma warning disable MA0182
/// <summary>Starts <see cref="GraphWorkflow"/> on Temporal with the run id as the workflow id; Temporal refuses a second start of the same id.</summary>
/// <param name="client">The Temporal client.</param>
/// <param name="taskQueue">The queue the worker polls.</param>
internal sealed class TemporalGraphRunLauncher(ITemporalClient client, string taskQueue) : IGraphRunLauncher
{
    /// <inheritdoc />
    public ValueTask<Result<bool>> Launch(WorkflowRun run, CancellationToken cancellationToken)
    {
        _ = (client, taskQueue, run, cancellationToken);
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182
