namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One row of GET /sindri/workflows/{slug}/runs. Status is read as a string, so a status this build doesn't know renders as itself.</summary>
/// <param name="Id">The run ("&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;").</param>
/// <param name="Revision">The revision it walked.</param>
/// <param name="Status">queued, running, waiting, succeeded, failed or cancelled.</param>
/// <param name="Trigger">What started it in one word: manual, cli, agent, schedule, or another stored kind as it is.</param>
/// <param name="StartedAt">When it was recorded.</param>
/// <param name="EndedAt">When it ended, or null while it has not.</param>
/// <param name="Error">Why it failed, as the engine wrote it, or null.</param>
/// <param name="Seconds">How long it took, or null while it runs or from a Sindri that does not say (#3683).</param>
/// <param name="Result">The one-line result ("tell failed: ..."), or null (#3683).</param>
/// <param name="FailureSentence">Why it failed in words, naming the step by its title, or null (absent when the error has none).</param>
internal sealed record WorkflowRunRowView(string Id, int Revision, string Status, string Trigger, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? Error, double? Seconds = null, string? Result = null, string? FailureSentence = null);
