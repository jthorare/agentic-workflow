namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>POST /sindri/projects/hide's body (projects page spec §5.3): hide or unhide a led project.</summary>
/// <param name="Linear">The project's "linear:project/&lt;uuid&gt;" key.</param>
/// <param name="Hidden">True to hide, false to unhide.</param>
/// <param name="Version">The hide row's version (0 for a project never hidden).</param>
internal sealed record ProjectHide(string Linear, bool Hidden, long Version);
