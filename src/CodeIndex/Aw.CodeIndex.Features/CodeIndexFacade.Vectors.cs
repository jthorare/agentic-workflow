using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Features;

// The stored-vectors read: the embedded symbols of a set of files, for the context map's glossary signal.
public sealed partial class CodeIndexFacade
{
    /// <inheritdoc />
    public async ValueTask<Result<SymbolVectorsView>> SymbolVectors(CodeIndexScope scope, RepoName repo, SymbolVectorsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Paths.Count is 0 or > CodeIndexRules.MaxSymbolVectorPaths || query.Paths.Any(string.IsNullOrWhiteSpace))
        {
            return CodeIndexErrors.QueryInvalid.With("ask for 1 to 2,000 non-blank paths");
        }

        var opened = await Open(scope, repo, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            var model = embedder.ModelFor(scope.Repos.First(r => r.Name == repo).Settings.Embeddings);
            var layer = opened.Value.Header.Layers.FirstOrDefault(l => l.Layer == LayerName.Embeddings);
            if (LayerError(layer, model) is { } unusable)
            {
                return unusable;
            }

            var paths = query.Paths.Distinct(StringComparer.Ordinal).ToList();
            var found = await opened.Value.SymbolVectors(paths, model, cancellationToken).ConfigureAwait(false);
            if (found.IsError)
            {
                return found.Error;
            }

            var have = found.Value.Select(v => v.Path).ToHashSet(StringComparer.Ordinal);
            return new SymbolVectorsView(Stamp(repo, opened.Value.Header, LayerName.Embeddings), model, found.Value, [.. paths.Where(p => !have.Contains(p))]);
        }
    }
}
