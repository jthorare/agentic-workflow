using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What one batch of a 'for_each' starts with.</summary>
/// <param name="Run">The run.</param>
/// <param name="ForEach">The 'for_each' node.</param>
/// <param name="First">The first item's index, counted from 1.</param>
/// <param name="Count">How many items this batch runs.</param>
internal sealed record GraphIterationInput(WorkflowRunId Run, NodeId ForEach, int First, int Count);
