namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>GET /sindri/projects: the cards in three groups, with the served empty sentence. The answer's asOf is not read: the status carries the freshness the app shows.</summary>
/// <param name="Empty">The sentence for an empty or unavailable list, or null.</param>
/// <param name="Viewer">The Linear viewer's name; null when Linear isn't connected.</param>
/// <param name="Freshness">One entry per source.</param>
/// <param name="Projects">The live cards.</param>
/// <param name="Finished">The Done and Canceled cards.</param>
/// <param name="Hidden">The cards the owner hid (filled with ?all=true).</param>
internal sealed record ProjectList(
    string? Empty,
    string? Viewer,
    IReadOnlyList<ProjectFreshnessView> Freshness,
    IReadOnlyList<ProjectCardView> Projects,
    IReadOnlyList<ProjectCardView> Finished,
    IReadOnlyList<ProjectCardView> Hidden);
