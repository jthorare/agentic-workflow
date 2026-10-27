namespace Aw.Judge.Features.ImportLabels;

/// <summary>The ImportLabels request ('judge label import [--question q] [--since 14d]').</summary>
/// <param name="Since">How far back to look for decided decisions (--since, default 14 days).</param>
/// <param name="Question">Only this question's decisions, or null for every question (--question).</param>
public sealed record ImportLabelsRequest(TimeSpan Since, string? Question = null);
