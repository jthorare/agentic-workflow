using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Memory;

/// <summary>A memory as an answer shows it.</summary>
/// <param name="Name">The name.</param>
/// <param name="Description">What it is for, or null.</param>
/// <param name="CreatedBy">The actor that made it.</param>
/// <param name="CreatedAt">When it was made.</param>
/// <param name="Facts">How many facts it holds.</param>
public sealed record MemoryView(string Name, string? Description, string CreatedBy, DateTimeOffset CreatedAt, int Facts)
{
    /// <summary>Shows a stored memory.</summary>
    /// <param name="memory">The memory.</param>
    /// <param name="facts">How many facts it holds.</param>
    /// <returns>The view.</returns>
    public static MemoryView From(MemoryInfo memory, int facts) => throw new NotSupportedException();
}

/// <summary>A fact as an answer shows it: never the embedding.</summary>
/// <param name="Key">The fact's key (on the wire: 'id').</param>
/// <param name="Text">The scrubbed text.</param>
/// <param name="Tags">The tags.</param>
/// <param name="Provenance">Where it came from.</param>
/// <param name="ExpiresAt">When it expires, or null.</param>
/// <param name="RefreshedAt">When it was last written.</param>
/// <param name="Score">The recall score; null outside a search.</param>
public sealed record FactView(
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
    public static FactView From(MemoryFact fact, float? score) => throw new NotSupportedException();
}
