using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>One run in the list.</summary>
/// <param name="Id">The run.</param>
/// <param name="Revision">The revision it walked.</param>
/// <param name="Status">'queued', 'running', 'waiting', 'succeeded', 'failed' or 'cancelled'.</param>
/// <param name="Trigger">What started it: the trigger's kind, such as 'manual' or 'schedule'.</param>
/// <param name="StartedAt">When it was recorded.</param>
/// <param name="EndedAt">When it ended, or null while it has not.</param>
/// <param name="Error">Why it failed, or null.</param>
internal sealed record WorkflowRunSummary(WorkflowRunId Id, int Revision, string Status, string Trigger, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? Error);
