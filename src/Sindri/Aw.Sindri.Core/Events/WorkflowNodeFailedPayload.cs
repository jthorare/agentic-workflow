using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.node.failed' payload (issue 2258, PR 6): a node failed.</summary>
/// <param name="RunId">The run's id.</param>
/// <param name="NodeId">The node.</param>
/// <param name="ItemIndex">The loop item, 0 outside a loop.</param>
/// <param name="Error">Why it failed.</param>
/// <param name="By">Who recorded it.</param>
public sealed record WorkflowNodeFailedPayload(WorkflowRunId RunId, NodeId NodeId, int ItemIndex, string Error, string By);
