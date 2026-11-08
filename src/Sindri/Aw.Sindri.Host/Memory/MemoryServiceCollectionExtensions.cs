using Aw.Embeddings;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host.ContextVectors;

namespace Aw.Sindri.Host.Memory;

/// <summary>Binds memory's embedding port to Aw.Embeddings (Sindri reaches it from the Host only), beside <see cref="ContextVectorsServiceCollectionExtensions"/>.</summary>
internal static class MemoryServiceCollectionExtensions
{
    /// <summary>Adds the memory embedder over the same pinned model and Ollama the code index uses, switched by the profile's <c xml:lang="en">index.embeddings.enabled</c>.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddMemoryEmbedder(this IServiceCollection services) => services
        .AddSingleton<IMemoryEmbedder>(static sp => new EmbeddingsMemoryEmbedder(new OllamaEmbedder(ContextVectorsServiceCollectionExtensions.DefaultOptions), ct => EmbeddingsOn(sp, ct)));

    // why: the live profile may be scoped, so each look opens its own scope; no profile or no setting leaves the default (on)
    private static async ValueTask<bool> EmbeddingsOn(IServiceProvider services, CancellationToken cancellationToken)
    {
        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var loaded = await scope.ServiceProvider.GetRequiredService<IProfileSource>().Load(cancellationToken).ConfigureAwait(false);
            return loaded.IsError || loaded.Value.Profile.Index?.Embeddings?.Enabled != false;
        }
    }
}
