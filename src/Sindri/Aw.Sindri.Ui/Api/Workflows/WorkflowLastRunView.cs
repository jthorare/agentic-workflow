namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>A workflow's last run as the list serves it (#3683; the API half is not shipped yet, so this is the frames' shape).</summary>
/// <param name="Run">The run id ("sum@1:499").</param>
/// <param name="Status">queued, running, waiting, succeeded, failed or cancelled.</param>
/// <param name="At">When it ended, or started while it runs.</param>
internal sealed record WorkflowLastRunView(string Run, string Status, DateTimeOffset At);
