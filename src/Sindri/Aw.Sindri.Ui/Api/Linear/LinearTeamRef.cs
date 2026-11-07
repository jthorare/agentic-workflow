namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The team an issue belongs to.</summary>
/// <param name="Id">The team's Linear id.</param>
/// <param name="Key">The team key, such as ENG.</param>
internal sealed record LinearTeamRef(string Id, string Key);
