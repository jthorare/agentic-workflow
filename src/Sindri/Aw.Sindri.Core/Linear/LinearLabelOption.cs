using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Linear;

/// <summary>A label in a team's lookups.</summary>
/// <param name="Uuid">Linear's id.</param>
/// <param name="Name">The label's name.</param>
/// <param name="Color">Its color as '#rrggbb'.</param>
public sealed record LinearLabelOption([property: JsonPropertyName("id")] string Uuid, string Name, string? Color);
