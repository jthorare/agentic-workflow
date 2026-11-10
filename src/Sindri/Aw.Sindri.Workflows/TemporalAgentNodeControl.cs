using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IAgentNodeControl"/> over Temporal (PR 7): the workflow whose id is the run id answers the 'agent-node' query and takes the 'agent-takeover', 'agent-handback' and 'agent-report' updates.
/// A run Temporal no longer knows has ended, and so has its node.
/// </summary>
/// <param name="connect">Gives the Temporal client (a seam so a test passes its own).</param>
/// <param name="queryLimit">How long the 'agent-node' query, connecting included, may take before it answers SND-API-001 (default 2 s): the run view asks it on every read, so a stuck server can't hang a read.</param>
internal sealed class TemporalAgentNodeControl(Func<CancellationToken, ValueTask<Result<ITemporalClient>>> connect, TimeSpan? queryLimit = null) : IAgentNodeControl
{
    private static readonly TimeSpan _defaultQueryLimit = TimeSpan.FromSeconds(2);

    /// <summary>A control that connects to the dev server 'sindri serve' started, once per call.</summary>
    /// <param name="frontend">Finds and connects to it.</param>
    public TemporalAgentNodeControl(TemporalFrontend frontend)
        : this(ct => (frontend ?? throw new ArgumentNullException(nameof(frontend))).Connect(flag: null, ct))
    {
    }

    /// <inheritdoc />
    public async ValueTask<Result<RunningAgentNode?>> Running(WorkflowRunId run, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(queryLimit ?? _defaultQueryLimit);
        var asked = await Ask(run, async (handle, token) => await handle.QueryAsync<AgentNodeView?>("agent-node", [], new WorkflowQueryOptions { Rpc = new RpcOptions { CancellationToken = token } }).ConfigureAwait(false), (limit.Token, cancellationToken)).ConfigureAwait(false);
        if (asked.IsError)
        {
            return asked.Error;
        }

        return asked.Value is { } view ? new RunningAgentNode(NodeId.From(view.Node), view.ItemIndex, LaunchId.From(view.Launch), view.TakenOver) : null;
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Takeover(WorkflowRunId run, NodeId node, int itemIndex, CancellationToken cancellationToken) =>
        Update(run, "agent-takeover", new AgentNodeCall(node.Value, itemIndex), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Handback(WorkflowRunId run, NodeId node, int itemIndex, CancellationToken cancellationToken) =>
        Update(run, "agent-handback", new AgentNodeCall(node.Value, itemIndex), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Report(WorkflowRunId run, AgentNodeReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        return Update(run, "agent-report", new AgentNodeDelivery(report.Node.Value, report.ItemIndex, report.Outputs, report.Human), cancellationToken);
    }

    private async ValueTask<Result<Unit>> Update(WorkflowRunId run, string name, object argument, CancellationToken cancellationToken)
    {
        var done = await Ask(run, async (handle, _) => await handle.ExecuteUpdateAsync<bool>(name, [argument]).ConfigureAwait(false), (cancellationToken, cancellationToken)).ConfigureAwait(false);
        return done.IsError ? done.Error : Unit.Value;
    }

    // tokens.Limit stops the call (the caller's token plus a time limit for the query); tokens.Caller is the caller's own: a stop from the limit alone is SND-API-001, a stop the caller asked for propagates.
    private async ValueTask<Result<T>> Ask<T>(WorkflowRunId run, Func<WorkflowHandle, CancellationToken, Task<T>> call, (CancellationToken Limit, CancellationToken Caller) tokens)
    {
        try
        {
            var client = await connect(tokens.Limit).ConfigureAwait(false);
            if (client.IsError)
            {
                return SindriErrors.EngineUnavailable.With(client.Error.Message);
            }

            return await call(client.Value.GetWorkflowHandle(run.Value), tokens.Limit).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!tokens.Caller.IsCancellationRequested)   // boundary: the query's time limit ran out, so the server is stuck or unreachable
        {
            return SindriErrors.EngineUnavailable.With("Temporal did not answer in time");
        }
        catch (WorkflowUpdateFailedException e)   // boundary: the workflow's validator refused the update, and its message says why
        {
            return SindriErrors.InvalidInput.With(e.InnerException?.Message ?? e.Message);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)   // boundary: Temporal has no such workflow, so the run ended or never was
        {
            return SindriErrors.GraphRunEnded.With($"Temporal has no running workflow for '{run.Value}'; the run has ended, so its node has too");
        }
        catch (RpcException e)   // boundary: the server is down or refused
        {
            return SindriErrors.EngineUnavailable.With(e.Message);
        }
    }
}
