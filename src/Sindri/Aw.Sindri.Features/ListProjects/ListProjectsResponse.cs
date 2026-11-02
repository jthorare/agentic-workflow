using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListProjects;

/// <summary>The Projects page (projects page spec §6.2).</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Empty">The page-level sentence (§3.1), or null.</param>
/// <param name="Viewer">Who Linear says the owner is, or null.</param>
/// <param name="Freshness">The freshness line's parts.</param>
/// <param name="Projects">The open cards, in page order.</param>
/// <param name="Finished">The Finished fold.</param>
/// <param name="Hidden">The Hidden fold.</param>
public sealed record ListProjectsResponse(
    AsOf AsOf,
    string? Empty,
    string? Viewer,
    IReadOnlyList<ProjectFreshnessRow> Freshness,
    IReadOnlyList<ProjectCard> Projects,
    IReadOnlyList<ProjectCard> Finished,
    IReadOnlyList<ProjectCard> Hidden);
