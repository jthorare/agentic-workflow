namespace Aw.Sindri.Core.Memory;

/// <summary>The answer of <see cref="IMemoryEmbedder"/>: vectors or the reason there are none.</summary>
/// <param name="Vectors">One vector per text, or null when unavailable.</param>
/// <param name="Reason">Why no model answered (informational, SND-MEM-004), or null.</param>
public sealed record MemoryEmbedding(IReadOnlyList<ReadOnlyMemory<float>>? Vectors, string? Reason)
{
    /// <summary>Gets a value indicating whether vectors came back.</summary>
    public bool IsAvailable => Vectors is not null;

    /// <summary>Wraps vectors.</summary>
    /// <param name="vectors">The vectors.</param>
    /// <returns>The answer.</returns>
    public static MemoryEmbedding Of(IReadOnlyList<ReadOnlyMemory<float>> vectors) => new(Vectors: vectors, Reason: null);

    /// <summary>Says no model answered.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>The answer.</returns>
    public static MemoryEmbedding Unavailable(string reason) => new(Vectors: null, Reason: reason);
}
