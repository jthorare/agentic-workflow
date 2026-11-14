using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>One run with what each of its nodes did.</summary>
/// <param name="Id">The run.</param>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision it walked.</param>
/// <param name="Status">'queued', 'running', 'waiting', 'succeeded', 'failed' or 'cancelled'.</param>
/// <param name="Trigger">What started it, as the stored JSON object ('kind', and the payload or the tick).</param>
/// <param name="Outputs">Its outputs, or null while none.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="ItemId">The item it works for, or null.</param>
/// <param name="StartedAt">When it was recorded.</param>
/// <param name="EndedAt">When it ended, or null while it has not.</param>
/// <param name="Nodes">The node runs, by node id then item index.</param>
internal sealed record GetWorkflowRunResponse(
    WorkflowRunId Id,
    WorkflowSlug Slug,
    int Revision,
    string Status,
    JsonNode? Trigger,
    JsonNode? Outputs,
    string? Error,
    ItemId? ItemId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<NodeRunView> Nodes)
{
    /// <summary>The trigger kind: 'manual', 'schedule', 'cli' or 'agent' (same words as the run list). Issue 3683.</summary>
    public string TriggerKind { get; init; } = "";
}
