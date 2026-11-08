using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.WriteMemoryFact;

/// <summary>What the write did.</summary>
/// <param name="Key">The fact's key (on the wire: 'id'); the existing fact's when it was refreshed.</param>
/// <param name="Action">'added', or 'refreshed' when a near duplicate with the same tags was found.</param>
public sealed record WriteMemoryFactResponse([property: JsonPropertyName("id")] string Key, string Action);
