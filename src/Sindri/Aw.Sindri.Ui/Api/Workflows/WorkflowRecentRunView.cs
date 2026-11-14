namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One of a workflow's last runs as the list serves it, newest first (#3683; the API half is not shipped yet).</summary>
/// <param name="Run">The run id.</param>
/// <param name="Status">queued, running, waiting, succeeded, failed or cancelled.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="Seconds">How long it took, or null while it runs.</param>
internal sealed record WorkflowRecentRunView(string Run, string Status, DateTimeOffset StartedAt, double? Seconds);
