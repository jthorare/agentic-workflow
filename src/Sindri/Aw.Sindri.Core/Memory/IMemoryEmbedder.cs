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
