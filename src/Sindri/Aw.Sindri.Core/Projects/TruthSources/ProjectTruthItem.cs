namespace Aw.Sindri.Core.Projects.TruthSources;

/// <summary>One work item, cut down to what the truth counts.</summary>
/// <param name="Repo">Its repo (owner/name), or null.</param>
/// <param name="Done">Done; a cancelled item is left out before this.</param>
/// <param name="LinkUrls">The urls its 'item-links' rows carry (PRs, issues).</param>
/// <param name="Item">The item's id, so a project that names it as a source counts it without a link (#4499); null when unknown.</param>
public sealed record ProjectTruthItem(string? Repo, bool Done, IReadOnlyList<string> LinkUrls, Ids.ItemId? Item = null);
