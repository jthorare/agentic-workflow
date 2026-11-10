using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What one batch of a 'for_each' starts with.</summary>
/// <param name="Run">The run.</param>
/// <param name="ForEach">The 'for_each' node.</param>
/// <param name="First">The first item's index, counted from 1.</param>
/// <param name="Count">How many items this batch runs.</param>
/// <param name="AgentLaunchAttempts">How many times a failing 'run_agent' launch of an item is tried before its node fails: the run's own cap, so every item has the same bound; 0 for the default.</param>
internal sealed record GraphIterationInput(WorkflowRunId Run, NodeId ForEach, int First, int Count, int AgentLaunchAttempts = 0);
