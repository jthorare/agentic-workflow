using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>One work item, cut down to what the truth counts.</summary>
/// <param name="Repo">Its repo (owner/name), or null.</param>
/// <param name="Done">Done; a cancelled item is left out before this.</param>
/// <param name="LinkUrls">The urls its 'item-links' rows carry (PRs, issues).</param>
internal sealed record ProjectTruthItem(string? Repo, bool Done, IReadOnlyList<string> LinkUrls);

/// <summary>One live agent session, cut down to what the truth counts.</summary>
/// <param name="Repo">Its repo (owner/name), or null.</param>
/// <param name="Running">Running or asking.</param>
internal sealed record ProjectTruthAgent(string? Repo, bool Running);

/// <summary>Everything <see cref="ProjectTruths.Build"/> reads.</summary>
/// <param name="Sources">The project's sources.</param>
/// <param name="Pulls">Every open PR record Sindri holds.</param>
/// <param name="Summaries">The newest per-head summary by "owner/name#number".</param>
/// <param name="Items">Every item with its links.</param>
/// <param name="Agents">Every live session.</param>
internal sealed record ProjectTruthInputs(
    ProjectSources Sources,
    IReadOnlyList<ReviewPull> Pulls,
    IReadOnlyDictionary<string, ReviewSummary> Summaries,
    IReadOnlyList<ProjectTruthItem> Items,
    IReadOnlyList<ProjectTruthAgent> Agents);
