using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>One work item, cut down to what the truth counts.</summary>
/// <param name="Repo">Its repo (owner/name), or null.</param>
/// <param name="Done">Done; a cancelled item is left out before this.</param>
/// <param name="LinkUrls">The urls its 'item-links' rows carry (PRs, issues).</param>
public sealed record ProjectTruthItem(string? Repo, bool Done, IReadOnlyList<string> LinkUrls);

/// <summary>One live agent session, cut down to what the truth counts.</summary>
/// <param name="Repo">Its repo (owner/name), or null.</param>
/// <param name="Running">Running or asking.</param>
public sealed record ProjectTruthAgent(string? Repo, bool Running);

/// <summary>What the page's truth is built from, read once for the union of every card's sources.</summary>
/// <param name="Pulls">Open PR records of those sources.</param>
/// <param name="Summaries">The newest per-head summary by "owner/name#number"; a PR with no entry has not been checked yet.</param>
/// <param name="Items">Every item with its links.</param>
/// <param name="Agents">Every live session.</param>
public sealed record ProjectTruthFacts(
    IReadOnlyList<ReviewPull> Pulls,
    IReadOnlyDictionary<string, ReviewSummary> Summaries,
    IReadOnlyList<ProjectTruthItem> Items,
    IReadOnlyList<ProjectTruthAgent> Agents);
