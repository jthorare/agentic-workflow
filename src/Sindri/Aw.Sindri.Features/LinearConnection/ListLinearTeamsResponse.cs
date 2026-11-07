namespace Aw.Sindri.Features.LinearConnection;

/// <summary>The teams a key can read.</summary>
/// <param name="Teams">The teams, in the order Linear gave them.</param>
internal sealed record ListLinearTeamsResponse(IReadOnlyList<LinearConnectionTeamView> Teams);
