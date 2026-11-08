using Aw.Embeddings;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Host.Memory;

/// <summary>
/// Memory's <see cref="IMemoryEmbedder"/> over <see cref="IEmbedder"/> (the pinned local model through Ollama, loopback only). Any embedder error, or a switched-off
/// context.index.embedding, comes back as <see cref="MemoryEmbedding.Unavailable"/> with the reason, never as an exception, so recall can fall back to keywords.
/// </summary>
/// <param name="embedder">The embedder.</param>
/// <param name="enabled">Whether the profile's embedding settings allow it (one switch turns the code index and memory off together).</param>
internal sealed class EmbeddingsMemoryEmbedder(IEmbedder embedder, Func<bool> enabled) : IMemoryEmbedder
{
    private readonly IEmbedder _embedder = embedder;
    private readonly Func<bool> _enabled = enabled;

    public ValueTask<MemoryEmbedding> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_embedder, _enabled);
        return new NotSupportedException();
    }
}
