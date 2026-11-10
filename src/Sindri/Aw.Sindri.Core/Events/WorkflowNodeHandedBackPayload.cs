using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.node.handed-back' payload (#2497): a person or an agent session handed a taken-over 'run_agent' node back.</summary>
/// <param name="RunId">The run's id.</param>
/// <param name="NodeId">The node.</param>
/// <param name="ItemIndex">The loop item, 0 outside a loop.</param>
/// <param name="By">Who did it: the caller's actor ('human:&lt;client&gt;', 'session:launch:&lt;launch id&gt;' or 'session:&lt;client&gt;').</param>
public sealed record WorkflowNodeHandedBackPayload(WorkflowRunId RunId, NodeId NodeId, int ItemIndex, string By);
