using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Tests.RunBacktest;

/// <summary>A repo reader that can't list the tree of the commit SPL-104's cut names (merge #2): git failing at one cut.</summary>
internal sealed class FailingReader(IRepoReader inner) : IRepoReader
{
    public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken) => inner.Head(repo, cancellationToken);

    public async ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        var listed = await inner.ListFiles(repo, commit, cancellationToken).ConfigureAwait(false);
        return listed.IsSuccess && listed.Value.Contains("src/layout/SplitPane.ts", StringComparer.Ordinal) && !listed.Value.Contains("docs/decisions/0001-orientation-toggle.md", StringComparer.Ordinal)
            ? ContextErrors.ExtractionUnreadable.With("git can't list this commit")   // the tree after PR #2 and before PR #3
            : listed;
    }

    public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken) => inner.Read(repo, commit, path, cancellationToken);

    public ValueTask<Result<IReadOnlyDictionary<string, string>>> BlobIds(ContextRepo repo, string commit, IReadOnlyList<string> paths, CancellationToken cancellationToken) => inner.BlobIds(repo, commit, paths, cancellationToken);

    public ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken) => inner.Changes(repo, range, cancellationToken);
}
