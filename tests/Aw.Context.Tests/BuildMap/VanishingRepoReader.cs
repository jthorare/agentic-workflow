using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Tests.BuildMap;

/// <summary>A repo reader whose one file can't be read although it is listed: a checkout that changed under the build.</summary>
internal sealed class VanishingRepoReader(IRepoReader inner, string vanished) : IRepoReader
{
    public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken) => inner.Head(repo, cancellationToken);

    public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken) => inner.ListFiles(repo, commit, cancellationToken);

    public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken) =>
        string.Equals(path, vanished, StringComparison.Ordinal)
            ? ValueTask.FromResult<Result<string>>(ContextErrors.ExtractionUnreadable.With("the file vanished"))
            : inner.Read(repo, commit, path, cancellationToken);

    public ValueTask<Result<IReadOnlyDictionary<string, string>>> BlobIds(ContextRepo repo, string commit, IReadOnlyList<string> paths, CancellationToken cancellationToken) => inner.BlobIds(repo, commit, paths, cancellationToken);

    public ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken) => inner.Changes(repo, range, cancellationToken);
}
