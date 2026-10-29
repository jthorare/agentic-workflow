namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>The merged page (projects page spec §3, §6.2): the open cards in order, then the two folded rows.</summary>
/// <param name="Empty">The page-level sentence (§3.1 states 3 to 6), or null.</param>
/// <param name="Viewer">Who Linear says the owner is, or null.</param>
/// <param name="Freshness">The freshness line's parts.</param>
/// <param name="Projects">In progress, Planned, Paused, Backlog (and no state last); inside each, newest last activity first.</param>
/// <param name="Finished">Done or Canceled in the last 14 days (all of them with 'all'), newest finish first.</param>
/// <param name="Hidden">Led projects the owner hid, by name.</param>
public sealed record ProjectsPage(
    string? Empty,
    string? Viewer,
    IReadOnlyList<ProjectFreshnessRow> Freshness,
    IReadOnlyList<ProjectCard> Projects,
    IReadOnlyList<ProjectCard> Finished,
    IReadOnlyList<ProjectCard> Hidden);
