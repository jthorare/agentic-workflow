using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Linear;

/// <summary>A Linear user as the strip names them.</summary>
/// <param name="Uuid">The user's Linear id.</param>
/// <param name="Name">Their name, from the team's member list.</param>
public sealed record LinearPerson([property: JsonPropertyName("id")] string Uuid, string Name);
