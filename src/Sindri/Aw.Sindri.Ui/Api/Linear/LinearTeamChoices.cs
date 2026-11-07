namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>200 of POST /sindri/linear/connection/teams: the teams the pasted read key can see.</summary>
/// <param name="Teams">The teams.</param>
internal sealed record LinearTeamChoices(IReadOnlyList<LinearTeam> Teams);
