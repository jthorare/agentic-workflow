namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>A workflow's last run as the list serves it (#3683).</summary>
/// <param name="Run">The run id ("sum@1:499").</param>
/// <param name="Status">queued, running, waiting, succeeded, failed or cancelled.</param>
/// <param name="At">When the store recorded the run.</param>
internal sealed record WorkflowLastRunView(string Run, string Status, DateTimeOffset At);
