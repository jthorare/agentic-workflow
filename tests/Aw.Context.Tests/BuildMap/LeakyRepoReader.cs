using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Kernel;

namespace Aw.Context.Tests.BuildMap;

/// <summary>A repo reader that ignores the repo's deny paths, lists and reads everything, and records every read: the build must filter on its own.</summary>
internal sealed class LeakyRepoReader(string head, IReadOnlyDictionary<string, string> files) : IRepoReader
{
    public List<string> Reads { get; } = [];

    public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(head);

    public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyList<string>>([.. files.Keys.Order(StringComparer.Ordinal)]));

    public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken)
    {
        Reads.Add(path);
        return ValueTask.FromResult<Result<string>>(files.TryGetValue(path, out var text) ? text : ContextErrors.ExtractionUnreadable.With("no such path"));
    }

    public ValueTask<Result<IReadOnlyDictionary<string, string>>> BlobIds(ContextRepo repo, string commit, IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyDictionary<string, string>>(paths.Where(files.ContainsKey).Distinct(StringComparer.Ordinal).ToDictionary(p => p, p => FakeRepoReader.BlobId(files[p]), StringComparer.Ordinal)));

    public ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<RepoChanges>>(ContextErrors.ExtractionUnreadable.With("this reader lists no changes"));
}
