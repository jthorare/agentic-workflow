namespace Aw.Sindri.Features.LinearIssues;

/// <summary>An issue's team.</summary>
/// <param name="Id">The team's Linear id.</param>
/// <param name="Key">The team's key, 'ENG'.</param>
internal sealed record LinearTeamView(string Id, string Key);
