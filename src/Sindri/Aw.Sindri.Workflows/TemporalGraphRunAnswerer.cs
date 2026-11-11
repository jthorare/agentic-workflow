using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Sends an answer as the 'Answer' update to the <see cref="GraphWorkflow"/> whose workflow id is the run id, or, for a loop item's question, to the batch's <see cref="GraphIterationWorkflow"/> its question recorded; the workflow's validator refuses an answer nothing waits for.</summary>
/// <param name="connect">Gives the Temporal client (a seam so a test passes its own).</param>
internal sealed class TemporalGraphRunAnswerer(Func<CancellationToken, ValueTask<Result<ITemporalClient>>> connect) : IGraphRunAnswerer
{
    /// <summary>An answerer that connects to the dev server 'sindri serve' started, once per answer.</summary>
    /// <param name="frontend">Finds and connects to it.</param>
    public TemporalGraphRunAnswerer(TemporalFrontend frontend)
        : this(ct => (frontend ?? throw new ArgumentNullException(nameof(frontend))).Connect(flag: null, ct))
    {
    }

    /// <summary>How long the update may take; a worker that does not poll would hold it for ever.</summary>
    internal static readonly TimeSpan UpdateLimit = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Answer(GraphAnswer answer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var client = await connect(cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return SindriErrors.EngineUnavailable.With(client.Error.Message);
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(UpdateLimit);
        try
        {
            var options = new WorkflowUpdateOptions { Rpc = new RpcOptions { CancellationToken = limit.Token } };
            _ = await client.Value.GetWorkflowHandle(answer.BatchWorkflow ?? answer.Run.Value).ExecuteUpdateAsync<string>("Answer", [new GraphAnswerInput(answer.Node.Value, answer.Text, answer.Choice, answer.By) { Item = answer.Item ?? 0 }], options).ConfigureAwait(false);
            return Unit.Value;
        }
        catch (Exception e) when (e is TemporalException or OperationCanceledException)   // boundary: Temporal said no, the server is down, or the worker did not answer in time
        {
            return AnswerFailure.Of(e, answer.Run.Value);
        }
    }
}
