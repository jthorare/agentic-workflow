namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The Linear feed's health, shown above the list.</summary>
/// <param name="State">ok, failing or rereading.</param>
/// <param name="LastSyncedAt">When the feed last synced; null before the first sync.</param>
/// <param name="Code">The error code while failing; null otherwise.</param>
/// <param name="Reread">The re-read progress while rereading; null otherwise.</param>
internal sealed record LinearFeed(string State, DateTimeOffset? LastSyncedAt, string? Code, LinearReread? Reread);
