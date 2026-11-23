using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Core.Projects.TruthSources;

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
