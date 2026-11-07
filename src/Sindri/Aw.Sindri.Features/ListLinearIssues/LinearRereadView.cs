namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>How far the feed's full re-read has got.</summary>
/// <param name="Done">Issues read so far.</param>
/// <param name="Total">Issues to read, or null when the store does not know (Context only counts what it has stored).</param>
internal sealed record LinearRereadView(int Done, int? Total);
