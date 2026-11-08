using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.RunWorkflow;

/// <summary>What a manual trigger became.</summary>
/// <param name="Run">The run's id (for a skip, the id it would have had).</param>
/// <param name="Status">'started', 'queued' or 'skipped'.</param>
/// <param name="Reason">Why it was skipped or queued, or null when it started.</param>
internal sealed record RunWorkflowResponse(WorkflowRunId Run, string Status, string? Reason);
