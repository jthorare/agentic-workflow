namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The rows of one project, or of one repo no project names (review page spec §6.1).</summary>
/// <param name="By">"project" or "repo".</param>
/// <param name="Id">The "prj_…" id for a project section; null for a repo one.</param>
/// <param name="Name">The project's name or the repo's owner/name.</param>
/// <param name="Rows">Newest activity first.</param>
internal sealed record ReviewSectionView(string By, string? Id, string Name, IReadOnlyList<ReviewRowView> Rows);
