namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>A Linear team.</summary>
/// <param name="Id">The team's Linear id.</param>
/// <param name="Key">The team key.</param>
/// <param name="Name">The team's name.</param>
internal sealed record LinearTeam(string Id, string Key, string Name);
