namespace Aw.Sindri.Core.Memory;

/// <summary>
/// Turns texts into vectors with the pinned local model. A missing model is a value (<see cref="MemoryEmbedding.Unavailable"/>), not an
/// exception, so recall can fall back to keywords and say so. Its adapter lives in the Host (Sindri reaches Aw.Embeddings from there only).
/// </summary>
public interface IMemoryEmbedder
{
    /// <summary>Embeds texts, in order.</summary>
    /// <param name="texts">The texts, already scrubbed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One vector per text, or Unavailable with the reason.</returns>
    public ValueTask<MemoryEmbedding> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}

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
    public static MemoryEmbedding Of(IReadOnlyList<ReadOnlyMemory<float>> vectors) => new(vectors, null);

    /// <summary>Says no model answered.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>The answer.</returns>
    public static MemoryEmbedding Unavailable(string reason) => new(null, reason);
}
