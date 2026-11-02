namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>POST /sindri/projects/{id}/detach's body (projects page spec §5.3): the version the sheet showed.</summary>
/// <param name="Version">The card's version.</param>
internal sealed record ProjectDetach(long Version);
