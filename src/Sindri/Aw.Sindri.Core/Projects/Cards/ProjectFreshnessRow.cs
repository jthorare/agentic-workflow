namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>One part of the freshness line (projects page spec §3.1): "Linear projects synced 4 min ago".</summary>
/// <param name="Source">The source id ('linear-projects', 'linear', 'github', or 'context' when Context itself failed).</param>
/// <param name="State">ok, syncing, failing.</param>
/// <param name="Text">The sentence part, such as "synced 4 min ago" or "needs token".</param>
/// <param name="At">When it last synced, or null.</param>
public sealed record ProjectFreshnessRow(string Source, ProjectFreshnessState State, string Text, DateTimeOffset? At);
