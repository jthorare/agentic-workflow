using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>A repo reader whose head or file listing fails with 048.</summary>
internal sealed class FailingRepo(IRepoReader inner, bool head, bool list) : IRepoReader
{
    private static Error Down => ContextErrors.ExtractionUnreadable.With("the test repo is down");

    public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken) =>
        head ? ValueTask.FromResult<Result<string>>(Down) : inner.Head(repo, cancellationToken);

    public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken) =>
        list ? ValueTask.FromResult<Result<IReadOnlyList<string>>>(Down) : inner.ListFiles(repo, commit, cancellationToken);

    public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken) => inner.Read(repo, commit, path, cancellationToken);

    public ValueTask<Result<IReadOnlyDictionary<string, string>>> BlobIds(ContextRepo repo, string commit, IReadOnlyList<string> paths, CancellationToken cancellationToken) => inner.BlobIds(repo, commit, paths, cancellationToken);

    public ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken) => inner.Changes(repo, range, cancellationToken);
}
