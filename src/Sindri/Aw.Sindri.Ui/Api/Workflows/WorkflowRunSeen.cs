namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflow-runs/{run}/seen: what the mark did.</summary>
/// <param name="Run">The run.</param>
/// <param name="Marked">True when the run was a failed live run and is now seen; false when there was nothing to mark.</param>
internal sealed record WorkflowRunSeen(string Run, bool Marked);
