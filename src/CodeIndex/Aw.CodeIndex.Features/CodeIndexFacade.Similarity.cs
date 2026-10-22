using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Similarity;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Features;

// The clones and embeddings reads: Similar from the clone rows, Neighbors from the stored vectors.
public sealed partial class CodeIndexFacade
{
    /// <inheritdoc />
    public async ValueTask<Result<SimilarAnswer>> Similar(CodeIndexScope scope, SymbolRef symbol, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (RepoOf(symbol) is not { } repo)
        {
            return CodeIndexErrors.SymbolNotFound.With("the ref names no repo");
        }

        var opened = await Open(scope, repo, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            var candidates = await opened.Value.CloneCandidates(symbol, cancellationToken).ConfigureAwait(false);
            if (candidates.IsError)
            {
                return candidates.Error;
            }

            var clones = Clones.Find(symbol, candidates.Value);
            return new SimilarAnswer(Stamp(repo, opened.Value.Header, LayerName.Clones), symbol, clones.Exact, clones.Near);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<NeighborsAnswer>> Neighbors(CodeIndexScope scope, RepoName repo, string text, int k, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text) || k is < 1 or > CodeIndexRules.MaxNeighbors)
        {
            return CodeIndexErrors.QueryInvalid.With("the text is empty, or k is outside 1 to 100");
        }

        var opened = await Open(scope, repo, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            var settings = scope.Repos.First(r => r.Name == repo).Settings.Embeddings;
            var layer = opened.Value.Header.Layers.FirstOrDefault(l => l.Layer == LayerName.Embeddings);
            if (LayerError(layer, embedder.ModelFor(settings)) is { } unusable)
            {
                return unusable;
            }

            var vectors = await embedder.Embed(settings, [text.Length <= CodeIndexRules.EmbedTextMaxCharacters ? text : text[..CodeIndexRules.EmbedTextMaxCharacters]], cancellationToken).ConfigureAwait(false);
            if (vectors.IsError)
            {
                return vectors.Error;
            }

            var nearest = await opened.Value.Nearest(vectors.Value[0], k, cancellationToken).ConfigureAwait(false);
            return nearest.IsError ? nearest.Error : new NeighborsAnswer(Stamp(repo, opened.Value.Header, LayerName.Embeddings), nearest.Value);
        }
    }

    // Why the embeddings layer cannot answer, or null when it can (ok, or pending with the vectors it has so far).
    private static Error? LayerError(LayerRecord? layer, string model)
    {
        if (layer is null or { State: LayerState.Disabled })
        {
            return CodeIndexErrors.IndexNotBuilt.With("the embeddings layer is not built for this repo");
        }

        if (layer.State == LayerState.Unavailable)
        {
            return new Error(layer.Code ?? CodeIndexErrors.EmbeddingServerFailed.Code, ErrorKind.Unavailable, "the embeddings layer is unavailable");
        }

        return string.Equals(layer.Stamp, model, StringComparison.Ordinal) ? null : CodeIndexErrors.PinMismatch.With("the index was embedded with another model than the profile names");
    }
}
