using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Tests.BuildMap;

/// <summary>A repo reader over several repos, each answered by its own reader.</summary>
internal sealed class RoutingRepoReader(IReadOnlyDictionary<string, IRepoReader> readers) : IRepoReader
{
    public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken) => For(repo)?.Head(repo, cancellationToken) ?? Missing<string>();

    public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken) =>
        For(repo)?.ListFiles(repo, commit, cancellationToken) ?? Missing<IReadOnlyList<string>>();

    public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken) =>
        For(repo)?.Read(repo, commit, path, cancellationToken) ?? Missing<string>();

    public ValueTask<Result<IReadOnlyDictionary<string, string>>> BlobIds(ContextRepo repo, string commit, IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        For(repo)?.BlobIds(repo, commit, paths, cancellationToken) ?? Missing<IReadOnlyDictionary<string, string>>();

    private IRepoReader? For(ContextRepo repo) => readers.GetValueOrDefault(repo.Name.Value);

    private static ValueTask<Result<T>> Missing<T>() => ValueTask.FromResult<Result<T>>(ContextErrors.ExtractionUnreadable.With("no such repo"));

    public ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken) =>
        For(repo)?.Changes(repo, range, cancellationToken) ?? Missing<RepoChanges>();
}
