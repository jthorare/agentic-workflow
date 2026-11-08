using Aw.Embeddings;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Host.ContextVectors;

namespace Aw.Sindri.Host.Memory;

/// <summary>Binds memory's embedding port to Aw.Embeddings (Sindri reaches it from the Host only), beside <see cref="ContextVectorsServiceCollectionExtensions"/>.</summary>
internal static class MemoryServiceCollectionExtensions
{
    /// <summary>Adds the memory embedder over the same pinned model and Ollama the code index uses.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddMemoryEmbedder(this IServiceCollection services) => services
        .AddSingleton<IMemoryEmbedder>(static _ => new EmbeddingsMemoryEmbedder(new OllamaEmbedder(ContextVectorsServiceCollectionExtensions.DefaultOptions), static () => true));
}
