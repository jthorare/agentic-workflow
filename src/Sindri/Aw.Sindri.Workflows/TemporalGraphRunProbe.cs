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
    /// <summary>A probe that connects to the dev server 'sindri serve' started, once per question.</summary>
    /// <param name="frontend">Finds and connects to it.</param>
    public TemporalGraphRunProbe(TemporalFrontend frontend)
        : this(ct => (frontend ?? throw new ArgumentNullException(nameof(frontend))).Connect(flag: null, ct))
    {
    }

    /// <summary>Whether a workflow in this status can still run: a closed one (completed, failed, canceled, terminated, timed out) cannot.</summary>
    /// <param name="status">The status Temporal describes.</param>
    /// <returns>False for a closed workflow.</returns>
    internal static bool IsOpen(WorkflowExecutionStatus status) => throw new NotSupportedException();

    /// <inheritdoc />
    public async ValueTask<Result<bool>> Exists(WorkflowRunId run, CancellationToken cancellationToken)
    {
        var client = await connect(cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return SindriErrors.EngineUnavailable.With(client.Error.Message);
        }

        try
        {
            _ = await client.Value.GetWorkflowHandle(run.Value).DescribeAsync().ConfigureAwait(false);
            return true;
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)   // boundary: Temporal has no such workflow
        {
            return false;
        }
        catch (RpcException e)   // boundary: the server is down or refused
        {
            return SindriErrors.EngineUnavailable.With(e.Message);
        }
    }
}
