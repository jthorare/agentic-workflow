using Aw.Context.Core.Vectors;
using Aw.Embeddings;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Host.ContextVectors;

/// <summary>Binds Context's vector ports to Aw.Embeddings (spec §9.2: Context can't reference it, so the Host is the glue).</summary>
internal static class ContextVectorsServiceCollectionExtensions
{
    /// <summary>The model and manifest digest of the 'ollama-model' row of context/code-index.lock.</summary>
    internal static readonly OllamaOptions DefaultOptions = new(EmbeddingSettings.DefaultUrl, "nomic-embed-text:latest", "sha256:0a109f422b47e3a30ba2b10eca18548e944e8a23073ee3f3e947efcf3c45e59f");

    private static string ModelKey { get; } = $"{DefaultOptions.Model}@{DefaultOptions.Digest}";

    /// <summary>Adds the query embedder, the chunk embedder and the vector scan; the embedder is built here and not registered as <see cref="IEmbedder"/>, which the code index owns a type of its own for.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddContextVectors(this IServiceCollection services) => services
        .AddSingleton<IQueryEmbedder>(static _ => new EmbeddingsQueryEmbedder(new OllamaEmbedder(DefaultOptions), ModelKey))
        .AddSingleton<IChunkEmbedder>(static _ => new EmbeddingsChunkEmbedder(new OllamaEmbedder(DefaultOptions), ModelKey))
        .AddSingleton<IVectorScan>(static _ => new EmbeddingsVectorScan(new ScanInProcess()))
        .Replace(ServiceDescriptor.Singleton<IRecordEmbedderProbe, OllamaRecordEmbedderProbe>());   // doctor's 'context:index-ollama' looks at the server the records embed with
}
