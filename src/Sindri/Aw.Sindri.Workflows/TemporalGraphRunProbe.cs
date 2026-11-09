using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Asks Temporal whether a workflow whose id is the run id exists (describe by id); not found is false, an unreachable server is SND-ENGINE-unavailable.</summary>
/// <param name="connect">Gives the Temporal client (a seam so a test passes its own).</param>
internal sealed class TemporalGraphRunProbe(Func<CancellationToken, ValueTask<Result<ITemporalClient>>> connect) : IGraphRunProbe
{
    private ITemporalClient? _client;

    /// <summary>A probe that connects to the dev server 'sindri serve' started on its first question and keeps the connection until an error.</summary>
    /// <param name="frontend">Finds and connects to it.</param>
    public TemporalGraphRunProbe(TemporalFrontend frontend)
        : this(ct => (frontend ?? throw new ArgumentNullException(nameof(frontend))).Connect(flag: null, ct))
    {
    }

    /// <summary>Whether a workflow in this status can still run: a closed one (completed, failed, canceled, terminated, timed out) cannot.</summary>
    /// <param name="status">The status Temporal describes.</param>
    /// <returns>False for a closed workflow.</returns>
    internal static bool IsOpen(WorkflowExecutionStatus status) =>
        status is not (WorkflowExecutionStatus.Completed or WorkflowExecutionStatus.Failed or WorkflowExecutionStatus.Canceled or WorkflowExecutionStatus.Terminated or WorkflowExecutionStatus.TimedOut);

    /// <summary>True when Temporal has a workflow with the run's id that is still open: a closed one (completed, failed, canceled, terminated, timed out) means the run's own workflow never settled the store.</summary>
    /// <param name="run">The run id.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    /// <returns>False when there is no such workflow or it is closed; an error when Temporal cannot be asked.</returns>
    public async ValueTask<Result<bool>> Exists(WorkflowRunId run, CancellationToken cancellationToken)
    {
        var client = _client;
        if (client is null)
        {
            var connected = await connect(cancellationToken).ConfigureAwait(false);
            if (connected.IsError)
            {
                return SindriErrors.EngineUnavailable.With(connected.Error.Message);
            }

            client = _client = connected.Value;   // why: one connection for the whole pass, not one per run
        }

        try
        {
            var described = await client.GetWorkflowHandle(run.Value).DescribeAsync().ConfigureAwait(false);
            return IsOpen(described.Status);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)   // boundary: Temporal has no such workflow
        {
            return false;
        }
        catch (RpcException e)   // boundary: the server is down or refused; the next question connects again
        {
            _client = null;
            return SindriErrors.EngineUnavailable.With(e.Message);
        }
    }
}
