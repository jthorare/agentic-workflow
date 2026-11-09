using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Asks Temporal to cancel the <see cref="GraphWorkflow"/> whose workflow id is the run id; the workflow ends the run as 'cancelled'.</summary>
/// <param name="connect">Gives the Temporal client (a seam so a test passes its own).</param>
/// <param name="runs">Says whether a run Temporal no longer knows has ended.</param>
internal sealed class TemporalGraphRunCanceller(Func<CancellationToken, ValueTask<Result<ITemporalClient>>> connect, IWorkflowRunStore runs) : IGraphRunCanceller
{
    /// <summary>A canceller that connects to the dev server 'sindri serve' started, once per cancel.</summary>
    /// <param name="frontend">Finds and connects to it.</param>
    /// <param name="runs">Says whether a run Temporal no longer knows has ended.</param>
    public TemporalGraphRunCanceller(TemporalFrontend frontend, IWorkflowRunStore runs)
        : this(ct => (frontend ?? throw new ArgumentNullException(nameof(frontend))).Connect(flag: null, ct), runs)
    {
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Cancel(WorkflowRunId run, CancellationToken cancellationToken)
    {
        var client = await connect(cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return SindriErrors.EngineUnavailable.With(client.Error.Message);
        }

        try
        {
            await client.Value.GetWorkflowHandle(run.Value).CancelAsync().ConfigureAwait(false);
            return Unit.Value;
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)   // boundary: Temporal has no such workflow, so the run row has nothing running behind it
        {
            // why: a run that just ended is gone from Temporal too; answer that it ended (SND-GRAPH-107), not that it never existed (SND-GRAPH-101)
            var row = await runs.Get(run, cancellationToken).ConfigureAwait(false);
            return row.IsSuccess && row.Value.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled
                ? SindriErrors.GraphRunEnded.With($"the run '{run.Value}' already ended; there is nothing to cancel")
                : SindriErrors.GraphRunNotFound.With($"Temporal has no running workflow for '{run.Value}'; it may have ended already");
        }
        catch (RpcException e)   // boundary: the server is down or refused
        {
            return SindriErrors.EngineUnavailable.With(e.Message);
        }
    }
}
