namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>One work item, cut down to what the truth counts.</summary>
/// <param name="Repo">Its repo (owner/name), or null.</param>
/// <param name="Done">Done; a cancelled item is left out before this.</param>
/// <param name="LinkUrls">The urls its 'item-links' rows carry (PRs, issues).</param>
public sealed record ProjectTruthItem(string? Repo, bool Done, IReadOnlyList<string> LinkUrls);
