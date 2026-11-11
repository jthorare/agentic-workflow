namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The activity classes the worker serves: the agent runtime's and the graph workflow's (issue 2258 PR 6). The recipe workflow is gone.</summary>
/// <param name="Agents">LaunchAgentStep, WatchAgentStep and CloseAgentStep (engine P8).</param>
/// <param name="Nodes">LaunchAgentNode, WatchAgentNode, RecoverAgentNode, CloseAgentNode and HoldAgentNode (a 'run_agent' node, PR 7).</param>
/// <param name="Graph">The graph workflow's activities (issue 2258, PR 6).</param>
internal sealed record WorkerActivities(AgentStepActivities Agents, AgentNodeActivities Nodes, GraphActivities Graph);
