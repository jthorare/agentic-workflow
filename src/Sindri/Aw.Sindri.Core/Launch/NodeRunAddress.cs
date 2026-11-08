using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>Which node run a launch belongs to: the workflow run, the node, and the item index of a 'for_each' iteration (0 outside one).</summary>
/// <param name="Run">The workflow run.</param>
/// <param name="Node">The 'run_agent' node.</param>
/// <param name="Index">The iteration's item index; 0 for a node outside a 'for_each'.</param>
public sealed record NodeRunAddress(WorkflowRunId Run, NodeId Node, int Index);
