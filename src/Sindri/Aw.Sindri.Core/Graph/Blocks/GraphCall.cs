using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One 'run_workflow' start.</summary>
/// <param name="Caller">The run that calls.</param>
/// <param name="Node">The calling node.</param>
/// <param name="Index">The node's item index inside a 'for_each', 0 otherwise.</param>
/// <param name="Target">The callee's slug.</param>
/// <param name="Payload">The payload the callee's 'workflow_call' trigger hands on, or null.</param>
/// <param name="Depth">The callee's depth: its caller's plus one.</param>
public sealed record GraphCall(WorkflowRunId Caller, NodeId Node, int Index, WorkflowSlug Target, JsonObject? Payload, int Depth)
{
    /// <summary>The slugs of the workflows up the call chain, oldest first, at most 'workflow.callDepth' of them; they travel in the callee's trigger as 'ancestors'.</summary>
    public IReadOnlyList<string> Ancestors { get; init; } = [];
}
