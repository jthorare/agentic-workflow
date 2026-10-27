using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Testing.Extract;

/// <summary>An in-memory repo at one commit that leaves out the paths its repo denies; <see cref="RepoReaderContract"/> holds it to the git reader's behavior.</summary>
/// <param name="known">The repo it answers for.</param>
/// <param name="head">Its only commit.</param>
/// <param name="files">The files at that commit.</param>
public sealed class FakeRepoReader(ContextRepo known, string head, IReadOnlyDictionary<string, string> files) : IRepoReader
{
    /// <inheritdoc/>
    public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string>>(Knows(repo) ? head : Unreadable);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Knows(repo) && commit == head ? Results.Success<IReadOnlyList<string>>([.. files.Keys.Where(f => !repo.Denies(f)).Order(StringComparer.Ordinal)]) : Unreadable);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> ListTracked(ContextRepo repo, string commit, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Knows(repo) && commit == head ? Results.Success<IReadOnlyList<string>>([.. files.Keys.Order(StringComparer.Ordinal)]) : Unreadable);

    /// <inheritdoc/>
    public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ValueTask.FromResult<Result<string>>(Knows(repo) && commit == head && !repo.Denies(path) && files.TryGetValue(path, out var text) ? text : Unreadable);
    }

    /// <summary>Tells the fake which commits exist and the answers its <see cref="Changes"/> gives (any other pair of known commits has no merge base).</summary>
    /// <param name="commits">The full shas of the commits it holds.</param>
    /// <param name="answers">One answer per range, keyed by its full base and head.</param>
    /// <returns>This fake.</returns>
    public FakeRepoReader WithHistory(IEnumerable<string> commits, IEnumerable<RepoChanges> answers)
    {
        ArgumentNullException.ThrowIfNull(commits);
        ArgumentNullException.ThrowIfNull(answers);
        _commits.AddRange(commits);
        _answers.AddRange(answers);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (!Knows(repo))
        {
            return ValueTask.FromResult<Result<RepoChanges>>(Unreadable);
        }

        var first = Expand(range.Base);
        var second = Expand(range.Head);
        if (first is null || second is null)
        {
            return ValueTask.FromResult<Result<RepoChanges>>(ContextErrors.CommitUnknown.With("the fake holds no such commit"));
        }

        var answer = _answers.Find(a => a.Base == first && a.Head == second);
        return ValueTask.FromResult<Result<RepoChanges>>(answer is not null ? answer : ContextErrors.NoMergeBase.With("the fake has no merge base for those commits"));
    }

    private string? Expand(string sha) => _commits.Find(c => c.StartsWith(sha, StringComparison.Ordinal));

    private readonly List<string> _commits = [];
    private readonly List<RepoChanges> _answers = [];

    private static Error Unreadable => ContextErrors.ExtractionUnreadable.With("the fake has no such repo, commit or path");

    private bool Knows(ContextRepo repo) => repo.Name == known.Name && string.Equals(repo.Path, known.Path, StringComparison.Ordinal);
}
