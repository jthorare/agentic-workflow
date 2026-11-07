namespace Aw.Sindri.Features.LinearIssues;

/// <summary>The Linear issue feed's health, shown above the list.</summary>
/// <param name="State">ok, failing or rereading.</param>
/// <param name="LastSyncedAt">When the feed's last tick ended; null before the first.</param>
/// <param name="Code">The code the last tick ended with while failing; null otherwise.</param>
/// <param name="Reread">The re-read's progress while rereading; null otherwise.</param>
internal sealed record LinearFeedView(string State, DateTimeOffset? LastSyncedAt, string? Code, LinearRereadView? Reread);
