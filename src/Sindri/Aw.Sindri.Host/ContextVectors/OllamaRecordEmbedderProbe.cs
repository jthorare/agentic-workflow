using Aw.Embeddings;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Host.ContextVectors;

/// <summary>Doctor's look at the records embedder: the same Ollama server, model and pinned digest the records are embedded with, checked without pulling or embedding anything.</summary>
internal sealed class OllamaRecordEmbedderProbe : IRecordEmbedderProbe, IDisposable
{
    private readonly OllamaEmbedder _embedder = new(ContextVectorsServiceCollectionExtensions.DefaultOptions);

    public string Model => ContextVectorsServiceCollectionExtensions.DefaultOptions.Model;

    public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => _embedder.Probe(cancellationToken);

    public void Dispose() => _embedder.Dispose();
}
