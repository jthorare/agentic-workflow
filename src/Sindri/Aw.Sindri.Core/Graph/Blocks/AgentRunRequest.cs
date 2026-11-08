using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One 'run_agent' node execution asking for a session.</summary>
/// <param name="RunId">The run.</param>
/// <param name="Node">The node.</param>
/// <param name="Config">The node's config with every reference replaced.</param>
public sealed record AgentRunRequest(WorkflowRunId RunId, NodeId Node, RunAgentConfig Config);
