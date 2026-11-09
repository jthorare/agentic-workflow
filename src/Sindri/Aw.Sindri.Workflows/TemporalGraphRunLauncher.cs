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
        catch (WorkflowAlreadyStartedException)   // boundary: Temporal already has this run id, so an earlier start of ours got through
        {
            return true;
        }
        catch (RpcException e)   // boundary: Temporal may have accepted the start before the reply was lost; that includes Unavailable, which a connection reset after the accept also returns
        {
            return await WasStarted(client.Value, run, e).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<bool>> WasStarted(ITemporalClient client, WorkflowRun run, RpcException startFailure)
    {
        try
        {
            _ = await client.GetWorkflowHandle(run.Id.Value).DescribeAsync().ConfigureAwait(false);
            return true;
        }
        catch (RpcException described) when (described.Code == RpcException.StatusCode.NotFound)   // boundary: Temporal has no such workflow
        {
            return SindriErrors.EngineUnavailable.With(startFailure.Message);
        }
        catch (RpcException described) when (described.Code == RpcException.StatusCode.Unavailable && startFailure.Code == RpcException.StatusCode.Unavailable)
        {
            // boundary: start and describe both found the server gone, so it most likely never took the start; reading it as launched would leave a StartNow run Running with no walk until the next serve start
            return SindriErrors.EngineUnavailable.With(startFailure.Message);
        }
        catch (RpcException)   // boundary: the describe failed too, so we can't tell
        {
            // why: failing a run that may be running leaves the walk going with a Failed row, while a Running row with no workflow is ended by the
            // next serve start's recovery pass; so an unknown answer reads as launched.
            return true;
        }
    }
}
