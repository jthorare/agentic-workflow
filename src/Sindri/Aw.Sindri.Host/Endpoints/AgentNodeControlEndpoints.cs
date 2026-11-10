using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.AgentNodeControl;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/workflow-runs/{run}/nodes/{node}/takeover and /handback (#2472): the human takes the 'run_agent' node a run waits on, and hands it back. The body is {"itemIndex": n} and may be left out for item 0;
/// the answer is {"run", "node", "itemIndex", "agent"} with 'taken_over' or 'waiting'.
/// </summary>
internal static class AgentNodeControlEndpoints
{
    internal static RouteGroupBuilder MapAgentNodeControl(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow-runs/{run}/nodes/{node}/takeover", static async (string run, string node, AgentNodeBody? body, IService<TakeOverAgentNodeRequest, AgentNodeControlled> service, CancellationToken ct) =>
            (await Call(run, node, (r, n) => service.Handle(new TakeOverAgentNodeRequest(r, n, body?.ItemIndex ?? 0), ct)).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflow-runs/{run}/nodes/{node}/handback", static async (string run, string node, AgentNodeBody? body, IService<HandBackAgentNodeRequest, AgentNodeControlled> service, CancellationToken ct) =>
            (await Call(run, node, (r, n) => service.Handle(new HandBackAgentNodeRequest(r, n, body?.ItemIndex ?? 0), ct)).ConfigureAwait(false)).ToHttpResult());
        return group;
    }

    // why: the answer turns into an IResult in the route lambdas, as CancelWorkflowRunEndpoint does; a named method calling HttpResults makes Host.Endpoints depend on Aw.Sindri.Host (ARCH-CYCLE-001)
    private static async ValueTask<Result<AgentNodeControlled>> Call(string run, string node, Func<WorkflowRunId, NodeId, ValueTask<Result<AgentNodeControlled>>> handle)
    {
        if (!WorkflowRunId.TryFrom(run, out var runId))
        {
            return SindriErrors.InvalidInput.With($"'{run}' is not a run id ('<slug>@<revision>:<occurrence>')");
        }

        if (!NodeId.TryFrom(node, out var nodeId))
        {
            return SindriErrors.InvalidInput.With($"'{node}' is not a node id (a lowercase letter, then lowercase letters, digits and '_')");
        }

        return await handle(runId, nodeId).ConfigureAwait(false);
    }
}
