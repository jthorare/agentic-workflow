using Aw.Embeddings;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Host.Memory;

/// <summary>
/// Memory's <see cref="IMemoryEmbedder"/> over <see cref="IEmbedder"/> (the pinned local model through Ollama, loopback only). A profile that switches embeddings off, an
/// unreachable server, a missing model or a wrong digest come back as <see cref="MemoryEmbedding.Unavailable"/> with the reason, never as an exception, so recall can fall back to keywords.
/// The embedder's own errors (AW-EMBEDDINGS-003, 004, 005) are the reachability check, so no separate probe call is made.
/// </summary>
/// <param name="embedder">The embedder.</param>
/// <param name="enabled">Whether the profile's <c xml:lang="en">index.embeddings.enabled</c> allows it (one switch turns the code index and memory off together).</param>
internal sealed class EmbeddingsMemoryEmbedder(IEmbedder embedder, Func<CancellationToken, ValueTask<bool>> enabled) : IMemoryEmbedder
{
    public async ValueTask<MemoryEmbedding> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (!await enabled(cancellationToken).ConfigureAwait(false))
        {
            return MemoryEmbedding.Unavailable("embeddings are switched off in the profile");
        }

        var embedded = await embedder.Embed(texts, cancellationToken).ConfigureAwait(false);
        if (embedded.IsError)
        {
            return MemoryEmbedding.Unavailable($"{embedded.Error.Code}: {embedded.Error.Message}");
        }

        return embedded.Value.Count == texts.Count
            ? MemoryEmbedding.Of(embedded.Value)
            : MemoryEmbedding.Unavailable("the embedder answered a different number of vectors than texts");
    }
}
