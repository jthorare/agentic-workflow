using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Glossary;

/// <summary>The stored embeddings of the code symbols in a repo's files, read from the code index. Read-only: it never embeds and never calls a model. A host with no code index gets a source that holds none.</summary>
public interface ISymbolVectors
{
    /// <summary>Reads the vectors of every embedded symbol in some files.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="paths">Repo-relative file paths, 1 to 2,000 of them.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The vectors found and the paths with none; an error when the repo has no index or its embeddings layer is off or unavailable.</returns>
    public ValueTask<Result<SymbolVectorsRead>> Read(ContextRepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken);
}
