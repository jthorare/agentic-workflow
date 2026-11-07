namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One piece of evidence found for the head.</summary>
internal sealed record ReviewEvidenceView(string Kind, string Label, string Path, string? Url, string MatchedBy, DateTimeOffset At);
