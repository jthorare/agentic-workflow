namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>GET /sindri/projects/{id}: one card with the viewer, the source freshness and whether the owner hid it. The answer's asOf is not read.</summary>
/// <param name="Viewer">The Linear viewer's name; null when Linear isn't connected.</param>
/// <param name="Freshness">One entry per source.</param>
/// <param name="Project">The card.</param>
/// <param name="Hidden">True when the owner hid the project.</param>
internal sealed record ProjectOne(string? Viewer, IReadOnlyList<ProjectFreshnessView> Freshness, ProjectCardView Project, bool Hidden);
