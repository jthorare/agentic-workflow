namespace Aw.Sindri.Ui.Api.Home;

/// <summary>One thing that makes the page less than whole: data, not an error.</summary>
/// <param name="Code">'source-stale', 'source-disconnected', 'brief-model-limited', 'brief-no-writer', 'brief-failed', 'lanes-unavailable' or 'behind'.</param>
/// <param name="Detail">What the page prints beside its label.</param>
internal sealed record HomeDegradedView(string Code, string Detail);
