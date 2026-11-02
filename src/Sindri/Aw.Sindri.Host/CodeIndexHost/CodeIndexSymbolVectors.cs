using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Glossary;
using Aw.Kernel;
using IndexSymbolVector = Aw.CodeIndex.Core.Facade.SymbolVector;
using SymbolVector = Aw.Context.Core.Glossary.SymbolVector;

namespace Aw.Sindri.Host.CodeIndexHost;

/// <summary>
/// Context's <see cref="ISymbolVectors"/> over the code index facade: the stored vectors of the symbols in some files, as the index's current embedding model made them. Read-only, nothing is embedded.
/// A repo outside the scope, one with no index and one whose embeddings layer is off or unavailable are the facade's errors (SND-CODEIDX-013, 006, 009, 005), which the map build turns into a degraded note.
/// </summary>
/// <param name="index">The code index.</param>
/// <param name="scopes">The scope the index is read in.</param>
internal sealed class CodeIndexSymbolVectors(ICodeIndex index, ICodeIndexScopeSource scopes) : ISymbolVectors
{
    public async ValueTask<Result<SymbolVectorsRead>> Read(ContextRepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (!RepoName.TryFrom(repo.Value, out var name))
        {
            return new SymbolVectorsRead([], paths);
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var found = await index.SymbolVectors(scope.Value, name, new SymbolVectorsQuery(paths), cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : new SymbolVectorsRead([.. found.Value.Vectors.Select(Row)], found.Value.Missing);
    }

    private static PathVector Row(IndexSymbolVector vector) => new(vector.Path, new SymbolVector(vector.Name, vector.Vector));
}
