using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Starts <see cref="GraphWorkflow"/> on Temporal with the run id as the workflow id; Temporal refuses a second start of the same id.</summary>
/// <param name="connect">Gives the Temporal client (a seam so a test passes its own).</param>
/// <param name="taskQueue">The queue the worker polls.</param>
internal sealed class TemporalGraphRunLauncher(Func<CancellationToken, ValueTask<Result<ITemporalClient>>> connect, string taskQueue) : IGraphRunLauncher
{
    /// <summary>A launcher that connects to the dev server 'sindri serve' started, once per launch.</summary>
    /// <param name="frontend">Finds and connects to it.</param>
    /// <param name="settings">The task queue.</param>
    public TemporalGraphRunLauncher(TemporalFrontend frontend, TemporalSettings settings)
        : this(ct => (frontend ?? throw new ArgumentNullException(nameof(frontend))).Connect(flag: null, ct), (settings ?? throw new ArgumentNullException(nameof(settings))).TaskQueue)
    {
    }

    /// <inheritdoc />
    public async ValueTask<Result<bool>> Launch(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var client = await connect(cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return SindriErrors.EngineUnavailable.With(client.Error.Message);
        }

        var options = new WorkflowOptions(run.Id.Value, taskQueue)
        {
            IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
            IdConflictPolicy = WorkflowIdConflictPolicy.Fail,
        };
        try
        {
            _ = await client.Value.StartWorkflowAsync((GraphWorkflow w) => w.RunAsync(new GraphWorkflowInput(run.Id)), options).ConfigureAwait(false);
            return true;
        }
        catch (WorkflowAlreadyStartedException)   // boundary: Temporal's answer to a second start of one run id
        {
            return SindriErrors.GraphRunIdTaken.With($"Temporal already has a workflow '{run.Id.Value}'; a trigger that fires twice is one run");
        }
        catch (RpcException e)   // boundary: the server is down or refused
        {
            return SindriErrors.EngineUnavailable.With(e.Message);
        }
    }
}
