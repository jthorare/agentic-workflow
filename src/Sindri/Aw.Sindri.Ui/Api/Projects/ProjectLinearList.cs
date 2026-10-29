namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>GET /sindri/projects/linear: the From Linear tab's rows (projects page spec §3.2, §6.2). The answer's asOf and each row's trust flag are not read: names are drawn as plain text.</summary>
/// <param name="Error">The one line Context failed with (the tab shows it in place of the rows), or null.</param>
/// <param name="Projects">Stored projects in scope that the owner doesn't lead, hasn't attached and aren't finished, by name.</param>
internal sealed record ProjectLinearList(string? Error, IReadOnlyList<ProjectLinearChoice> Projects);
