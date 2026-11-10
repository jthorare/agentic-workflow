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
internal sealed class TemporalAgentNodeControl(Func<CancellationToken, ValueTask<Result<ITemporalClient>>> connect) : IAgentNodeControl
{
    /// <summary>A control that connects to the dev server 'sindri serve' started, once per call.</summary>
    /// <param name="frontend">Finds and connects to it.</param>
    public TemporalAgentNodeControl(TemporalFrontend frontend)
        : this(ct => (frontend ?? throw new ArgumentNullException(nameof(frontend))).Connect(flag: null, ct))
    {
    }

    /// <inheritdoc />
    public async ValueTask<Result<RunningAgentNode?>> Running(WorkflowRunId run, CancellationToken cancellationToken)
    {
        var asked = await Ask(run, async handle => await handle.QueryAsync<AgentNodeView?>("agent-node", []).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
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
        var done = await Ask(run, async handle => await handle.ExecuteUpdateAsync<bool>(name, [argument]).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : Unit.Value;
    }

    private async ValueTask<Result<T>> Ask<T>(WorkflowRunId run, Func<WorkflowHandle, Task<T>> call, CancellationToken cancellationToken)
    {
        var client = await connect(cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return SindriErrors.EngineUnavailable.With(client.Error.Message);
        }

        try
        {
            return await call(client.Value.GetWorkflowHandle(run.Value)).ConfigureAwait(false);
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
