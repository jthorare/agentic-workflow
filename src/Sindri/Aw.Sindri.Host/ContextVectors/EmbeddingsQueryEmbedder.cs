using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Embeddings;
using Aw.Kernel;

namespace Aw.Sindri.Host.ContextVectors;

/// <summary>Context's <see cref="IQueryEmbedder"/> over <see cref="IEmbedder"/>: one text embedded with the pinned model and L2-normalised (spec §4.4, §5.6).</summary>
internal sealed class EmbeddingsQueryEmbedder(IEmbedder embedder, string model) : IQueryEmbedder
{
    private const int MaxChars = 8000;

    public async ValueTask<Result<QueryEmbedding>> Embed(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxChars)
        {
            return ContextErrors.QueryInvalid.With("the text to embed must be 1 to 8,000 characters");
        }

        var embedded = await embedder.Embed([text], cancellationToken).ConfigureAwait(false);
        if (embedded.IsError)
        {
            // the dense leg drops out whatever the reason (target refused, server down, model missing or not the pinned digest); the code of the cause stays in the message
            return ContextErrors.EmbedderUnavailable.With($"{embedded.Error.Code}: {embedded.Error.Message}");
        }

        if (embedded.Value.Count != 1)
        {
            return ContextErrors.EmbedderUnavailable.With("the embedder answered a different number of vectors than texts");
        }

        var vec = embedded.Value[0].ToArray();
        return VectorMath.Normalize(vec)
            ? new QueryEmbedding(model, vec)
            : ContextErrors.EmbedderUnavailable.With("the embedder answered a zero or non-finite vector");
    }
}
