namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflow-runs/{run}/cancel: the cancel was delivered; the run ends as cancelled when its workflow stops.</summary>
/// <param name="Run">The run.</param>
internal sealed record WorkflowRunCancelled(string Run);
