using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Sends an answer to the <see cref="GraphWorkflow"/> whose workflow id is the run id as its 'Answer' update; the workflow's validator refuses an answer nothing waits for.</summary>
/// <param name="connect">Gives the Temporal client (a seam so a test passes its own).</param>
internal sealed class TemporalGraphRunAnswerer(Func<CancellationToken, ValueTask<Result<ITemporalClient>>> connect) : IGraphRunAnswerer
{
    /// <summary>An answerer that connects to the dev server 'sindri serve' started, once per answer.</summary>
    /// <param name="frontend">Finds and connects to it.</param>
    public TemporalGraphRunAnswerer(TemporalFrontend frontend)
        : this(ct => (frontend ?? throw new ArgumentNullException(nameof(frontend))).Connect(flag: null, ct))
    {
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Answer(GraphAnswer answer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var client = await connect(cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return SindriErrors.EngineUnavailable.With(client.Error.Message);
        }

        try
        {
            _ = await client.Value.GetWorkflowHandle(answer.Run.Value).ExecuteUpdateAsync<string>("Answer", [new GraphAnswerInput(answer.Node.Value, answer.Text, answer.Choice, answer.By)]).ConfigureAwait(false);
            return Unit.Value;
        }
        catch (WorkflowUpdateFailedException e) when (e.InnerException is ApplicationFailureException refused)   // boundary: the workflow's validator said no
        {
            return string.Equals(refused.ErrorType, AnswerRefusal.NotWaiting, StringComparison.Ordinal)
                ? SindriErrors.GraphNotWaiting.With(refused.Message)
                : SindriErrors.InvalidInput.With(refused.Message);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)   // boundary: Temporal has no such workflow, so the run ended
        {
            return SindriErrors.GraphRunEnded.With($"the run '{answer.Run.Value}' already ended; there is nothing to answer");
        }
        catch (RpcException e)   // boundary: the server is down or refused
        {
            return SindriErrors.EngineUnavailable.With(e.Message);
        }
    }
}
