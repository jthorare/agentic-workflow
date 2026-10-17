using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>One row of `docker compose ps --format json` (Compose's own PascalCase wire format; every field optional so a newer Compose adding or dropping one still reads).</summary>
internal sealed record ComposePsRow(
    [property: JsonPropertyName("Service")] string? Service,
    [property: JsonPropertyName("State")] string? State,
    [property: JsonPropertyName("Health")] string? Health,
    [property: JsonPropertyName("Image")] string? Image,
    [property: JsonPropertyName("Publishers")] IReadOnlyList<ComposePsPublisher>? Publishers);
