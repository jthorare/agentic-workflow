using Aw.Kernel;

namespace Aw.Embeddings;

/// <summary>Turns texts into vectors with the pinned local model. The Host binds Context's query-embedder port to it.</summary>
public interface IEmbedder
{
    /// <summary>The check done before embedding anything, with no work: target allowed, model installed, digest as pinned.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Success, AW-EMBEDDINGS-003 (target refused), 004 (server failed) or 005 (model missing or not the pinned digest).</returns>
    public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken);

    /// <summary>Embeds texts, in order. The vectors are the model's own, not normalised.</summary>
    /// <param name="texts">The texts, already scrubbed and cut.</param>
    /// <param name="cancellationToken">Cancels the call; the cancellation is thrown, not returned.</param>
    /// <returns>One vector per text; AW-EMBEDDINGS-003, 004 or 005.</returns>
    public ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
