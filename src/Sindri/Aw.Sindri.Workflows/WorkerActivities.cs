namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The activity classes the worker serves. The recipe workflow is gone, so only the agent runtime's remain until the graph workflow arrives (issue 2258 PR 6).</summary>
/// <param name="Agents">LaunchAgentStep, WatchAgentStep and CloseAgentStep (engine P8).</param>
internal sealed record WorkerActivities(AgentStepActivities Agents);
