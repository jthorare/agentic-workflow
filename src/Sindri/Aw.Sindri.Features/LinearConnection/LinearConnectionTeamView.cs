namespace Aw.Sindri.Features.LinearConnection;

/// <summary>A Linear team as the Connections page lists it.</summary>
/// <param name="Id">Linear's id for the team; null while Sindri has not stored the team yet.</param>
/// <param name="Key">The team key, such as 'ENG'.</param>
/// <param name="Name">The team's name; null while Sindri has not stored the team yet.</param>
internal sealed record LinearConnectionTeamView(string? Id, string Key, string? Name);
