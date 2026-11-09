using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Helpers for the 'run_agent' workflow tests (PR 7): a spec with one agent node, and reading what it recorded.</summary>
internal static class AgentNodeRig
{
    public static readonly NodeId Agent = NodeId.From("agent");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A manual trigger and one 'run_agent' node that declares <paramref name="outputs"/>.</summary>
    public static GraphSpec Spec(string outputs = "{}") => GraphRig.Spec("unlimited",
        [GraphRig.Node("start", "manual"), GraphRig.Node("agent", "run_agent", config: new JsonObject { ["repo"] = "web", ["prompt"] = "check it", ["outputs"] = JsonNode.Parse(outputs) })],
        [GraphRig.Edge("start", "agent")]);

    /// <summary>Defines the spec and starts one run of it.</summary>
    public static async Task<WorkflowRunId> Start(GraphRig rig, string outputs = "{}")
    {
        var workflow = await rig.Define(Spec(outputs));
        return (await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId;
    }

    /// <summary>The agent node's row.</summary>
    public static async Task<NodeRun> Row(GraphRig rig, WorkflowRunId run) =>
        (await rig.Runs.NodeRuns(run, Token)).Value.Single(static n => n.NodeId == Agent);
}
