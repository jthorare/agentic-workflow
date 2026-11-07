using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Linear;

/// <summary>A team member in a team's lookups.</summary>
/// <param name="Uuid">The user's Linear id.</param>
/// <param name="Name">Their name.</param>
/// <param name="Active">False for a member who left the team.</param>
public sealed record LinearMemberOption([property: JsonPropertyName("id")] string Uuid, string Name, bool Active);
