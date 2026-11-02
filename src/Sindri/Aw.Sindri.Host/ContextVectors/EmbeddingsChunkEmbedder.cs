using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Embeddings;
using Aw.Kernel;

namespace Aw.Sindri.Host.ContextVectors;

/// <summary>Context's <see cref="IChunkEmbedder"/> over <see cref="IEmbedder"/>: a batch of chunk texts embedded with the pinned model (spec §5.6). The store L2-normalises on write.</summary>
internal sealed class EmbeddingsChunkEmbedder(IEmbedder embedder, string model) : IChunkEmbedder
{
    private const int MaxBatch = 32;

    public string Model => model;

    public async ValueTask<Result<ChunkEmbeddings>> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count is < 1 or > MaxBatch)
        {
            return ContextErrors.QueryInvalid.With("a batch embeds 1 to 32 texts");
        }

        var embedded = await embedder.Embed(texts, cancellationToken).ConfigureAwait(false);
        if (embedded.IsError)
        {
            // the job stops whatever the reason (target refused, server down, model missing or not the pinned digest); the code of the cause stays in the message
            return ContextErrors.EmbedderUnavailable.With($"{embedded.Error.Code}: {embedded.Error.Message}");
        }

        return embedded.Value.Count == texts.Count
            ? new ChunkEmbeddings(model, embedded.Value)
            : ContextErrors.EmbedderUnavailable.With("the embedder answered a different number of vectors than texts");
    }
}
