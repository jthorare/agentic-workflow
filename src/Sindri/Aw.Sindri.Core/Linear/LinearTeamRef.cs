using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Linear;

/// <summary>A Linear team as the app names it (GET /sindri/linear/lookups/{team}).</summary>
/// <param name="Uuid">Linear's id for the team; null for a team never synced.</param>
/// <param name="Key">The team's key, 'ENG'.</param>
/// <param name="Name">The team's name; null for a team never synced.</param>
public sealed record LinearTeamRef([property: JsonPropertyName("id")] string? Uuid, string Key, string? Name);
