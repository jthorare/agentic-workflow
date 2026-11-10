using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IBoardStore.</summary>
public sealed class InMemoryBoardStore : IBoardStore
{
    private readonly Dictionary<string, BoardSeen> _seen = new(StringComparer.Ordinal);
    private BoardFacts? _board;

    /// <inheritdoc />
    public ValueTask<Result<Unit>> RecordMain(string sha, IReadOnlyList<int> mergedPrs, DateTimeOffset at, CancellationToken cancellationToken)
    {
        // The same sha again (merge-run, then the watch seeing main move) is not a new move: same version, the pull requests joined.
        _board = _board is { } same && string.Equals(same.MainSha, sha, StringComparison.Ordinal)
            ? same with { MergedPrs = [.. same.MergedPrs.Union(mergedPrs)] }
            : new BoardFacts(sha, [.. mergedPrs], at, (_board?.Version ?? 0) + 1);
        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<BoardFacts?>> Read(CancellationToken cancellationToken) => Done.Of(_board);

    /// <inheritdoc />
    public ValueTask<Result<BoardSeen?>> SeenVersion(string lane, CancellationToken cancellationToken) =>
        Done.Of(_seen.TryGetValue(lane, out var seen) ? seen : null);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkSeen(string lane, BoardSeen seen, CancellationToken cancellationToken)
    {
        _seen[lane] = seen;
        return Done.Unit();
    }
}
