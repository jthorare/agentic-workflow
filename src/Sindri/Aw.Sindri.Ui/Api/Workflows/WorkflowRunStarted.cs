namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflows/{slug}/runs: what the manual trigger became.</summary>
/// <param name="Run">The run's id (for a skip, the id it would have had).</param>
/// <param name="Status">"started", "queued" or "skipped".</param>
/// <param name="Reason">Why it was queued or skipped, or null when it started.</param>
internal sealed record WorkflowRunStarted(string Run, string Status, string? Reason);
