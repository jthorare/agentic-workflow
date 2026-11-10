using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>Where main stands (its sha and the pull requests that just merged into it) and what each lane has already seen of the team board.</summary>
public interface IBoardStore
{
    /// <summary>Records that main moved; the board version goes up by one.</summary>
    /// <param name="sha">Main's new sha.</param>
    /// <param name="mergedPrs">The pull requests that merged into main with this move.</param>
    /// <param name="at">When main moved.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> RecordMain(string sha, IReadOnlyList<int> mergedPrs, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Records main as the first move, only when no move was recorded yet (a fresh install's watch start); a board that holds a move is left as it is.</summary>
    /// <param name="sha">Main's sha as the watch found it.</param>
    /// <param name="at">When the watch found it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when this call wrote the first move, false when the board already held one.</returns>
    public ValueTask<Result<bool>> SeedMain(string sha, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>The latest recorded move of main.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The facts; null before any move was recorded.</returns>
    public ValueTask<Result<BoardFacts?>> Read(CancellationToken cancellationToken);

    /// <summary>What a lane last saw of the board.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The mark; null when the lane never saw the board.</returns>
    public ValueTask<Result<BoardSeen?>> SeenVersion(string lane, CancellationToken cancellationToken);

    /// <summary>Records what a lane has now seen of the board.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="seen">The board and lane map versions shown.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> MarkSeen(string lane, BoardSeen seen, CancellationToken cancellationToken);
}
