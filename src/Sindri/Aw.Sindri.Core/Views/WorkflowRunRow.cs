using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Views;

/// <summary>One graph run in the 'workflow-runs' view (issue 2258, PR 6), folded from its 'workflow.run.*' and 'workflow.node.*' events.</summary>
/// <param name="Id">The run id, '&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;'; the row's key.</param>
/// <param name="WorkflowId">The workflow.</param>
/// <param name="Revision">The revision the run walks.</param>
/// <param name="Trigger">What started it, as JSON.</param>
/// <param name="Status">'running', 'succeeded', 'failed' or 'cancelled'.</param>
/// <param name="Error">Why it failed (or why it was cancelled), or null.</param>
/// <param name="StartedAt">When 'workflow.run.started' was recorded.</param>
/// <param name="EndedAt">When the run finished or was cancelled, or null while it runs.</param>
/// <param name="LastNodeId">The node of the newest node event, or null before any.</param>
/// <param name="ItemId">The item the run works for, or null.</param>
/// <param name="Sequence">The ledger sequence of the newest event folded into the row.</param>
/// <param name="Mode">'live' or 'test' (<see cref="Graph.Runs.WorkflowRunMode"/>), from 'workflow.run.started'; a row folded before the field existed reads live.</param>
public sealed record WorkflowRunRow(WorkflowRunId Id, WorkflowId WorkflowId, int Revision, string Trigger, string Status, string? Error, DateTimeOffset StartedAt, DateTimeOffset? EndedAt,
    NodeId? LastNodeId, ItemId? ItemId, long Sequence, string Mode = Graph.Runs.WorkflowRunMode.Live);
