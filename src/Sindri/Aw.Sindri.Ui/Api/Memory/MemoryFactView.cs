using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>One fact as the API shows it.</summary>
/// <param name="Key">The fact's key (on the wire: 'id').</param>
/// <param name="Text">The text.</param>
/// <param name="Tags">The tags.</param>
/// <param name="Provenance">Where it came from.</param>
/// <param name="ExpiresAt">When it expires, or null.</param>
/// <param name="RefreshedAt">When it was last written.</param>
/// <param name="Score">The recall score; null outside a search.</param>
internal sealed record MemoryFactView(
    [property: JsonPropertyName("id")] string Key,
    string Text,
    IReadOnlyList<string> Tags,
    MemoryProvenance Provenance,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset RefreshedAt,
    float? Score);
