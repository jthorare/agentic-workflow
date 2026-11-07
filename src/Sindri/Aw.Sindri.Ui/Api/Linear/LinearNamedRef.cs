namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>An issue's project.</summary>
/// <param name="Id">The project's Linear id.</param>
/// <param name="Name">The project's name.</param>
internal sealed record LinearNamedRef(string Id, string Name);
