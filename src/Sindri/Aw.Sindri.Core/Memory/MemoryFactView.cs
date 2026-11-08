using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Memory;

/// <summary>A fact as an answer shows it: never the embedding.</summary>
/// <param name="Key">The fact's key (on the wire: 'id').</param>
/// <param name="Text">The scrubbed text.</param>
/// <param name="Tags">The tags.</param>
/// <param name="Provenance">Where it came from.</param>
/// <param name="ExpiresAt">When it expires, or null.</param>
/// <param name="RefreshedAt">When it was last written.</param>
/// <param name="Score">The recall score; null outside a search.</param>
public sealed record MemoryFactView(
    [property: JsonPropertyName("id")] string Key,
    string Text,
    IReadOnlyList<string> Tags,
    MemoryProvenance Provenance,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset RefreshedAt,
    float? Score)
{
    /// <summary>Shows a stored fact.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="score">The recall score, or null.</param>
    /// <returns>The view.</returns>
    public static MemoryFactView From(MemoryFact fact, float? score)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return new MemoryFactView(fact.Key, fact.Text, fact.Tags, fact.Provenance, fact.ExpiresAt, fact.RefreshedAt, score);
    }
}
