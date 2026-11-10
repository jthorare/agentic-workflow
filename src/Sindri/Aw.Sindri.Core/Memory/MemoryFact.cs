using Aw.Kernel;

namespace Aw.Sindri.Core.Memory;

/// <summary>One fact in a memory: short text with tags, an optional expiry and where it came from.</summary>
/// <param name="Key">The fact's key, unique in the ledger.</param>
/// <param name="Store">The memory it belongs to.</param>
/// <param name="Text">The text, already scrubbed (at most <see cref="MaxTextLength"/> characters).</param>
/// <param name="Tags">The tags, lowercase, sorted, no repeats. Part of the fact's identity for deduplication.</param>
/// <param name="Provenance">Where it came from.</param>
/// <param name="ExpiresAt">When the hourly service deletes it, or null for never.</param>
/// <param name="CreatedAt">When it was first written.</param>
/// <param name="RefreshedAt">When it was last written again (equal to CreatedAt until a near duplicate refreshes it).</param>
/// <param name="Embedding">The text's vector, or empty when no model was available at write time.</param>
/// <param name="Model">The model (name and digest) that made the vector, or null when there is none.</param>
/// <param name="WrittenBy">The actor of the call that wrote the fact (its caller, never the request), or null for a fact written before this field existed.</param>
public sealed record MemoryFact(
    string Key,
    MemoryName Store,
    string Text,
    IReadOnlyList<string> Tags,
    MemoryProvenance Provenance,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset RefreshedAt,
    ReadOnlyMemory<float> Embedding,
    string? Model = null,
    string? WrittenBy = null)
{
    /// <summary>Gets the dimension of the stored vector; 0 when the fact has none yet.</summary>
    public int Dimension => Embedding.Length;

    /// <summary>The longest fact text: 2,000 characters.</summary>
    public const int MaxTextLength = 2000;

    /// <summary>Checks a fact text before it is scrubbed and stored.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, or SND-MEM-003 when it is empty or longer than <see cref="MaxTextLength"/>.</returns>
    public static Result<string> CheckText(string text) =>
        string.IsNullOrWhiteSpace(text) || text.Length > MaxTextLength
            ? SindriErrors.MemoryFactInvalid.With("A fact is 1 to 2,000 characters of text.")
            : text;

    /// <summary>Whether the fact has expired.</summary>
    /// <param name="now">The time to compare with.</param>
    /// <returns>True when ExpiresAt is set and not after <paramref name="now"/>.</returns>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } at && at <= now;
}
