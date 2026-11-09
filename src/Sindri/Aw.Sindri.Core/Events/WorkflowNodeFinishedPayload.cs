using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.node.finished' payload (issue 2258, PR 6): a node finished or was skipped.</summary>
/// <param name="RunId">The run's id.</param>
/// <param name="NodeId">The node.</param>
/// <param name="ItemIndex">The loop item, 0 outside a loop.</param>
/// <param name="Status">'succeeded' or 'skipped'.</param>
/// <param name="Truncated">True when its input or output was cut at the cap.</param>
/// <param name="By">Who recorded it.</param>
public sealed record WorkflowNodeFinishedPayload(WorkflowRunId RunId, NodeId NodeId, int ItemIndex, string Status, bool Truncated, string By);
