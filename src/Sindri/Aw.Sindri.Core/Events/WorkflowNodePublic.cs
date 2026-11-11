using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'workflow.node.started', '.finished' and '.failed' (issue 2823): which node of which run, and the loop item.</summary>
/// <param name="RunId">The run.</param>
/// <param name="NodeId">The node.</param>
/// <param name="ItemIndex">The loop item index.</param>
public sealed record WorkflowNodePublic(WorkflowRunId RunId, NodeId NodeId, int ItemIndex);
