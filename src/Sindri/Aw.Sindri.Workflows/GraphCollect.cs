using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'GraphCollect' activity gathers: the values the 'collect' node gave for each item of a finished loop.</summary>
/// <param name="Run">The run.</param>
/// <param name="Collect">The 'collect' node.</param>
/// <param name="Skipped">How many items a body node with 'onFailure: skip-item' dropped (#3806); 0 in a history written before it.</param>
internal sealed record GraphCollect(WorkflowRunId Run, NodeId Collect, int Skipped = 0);
