using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>IBoardStore over the key-value store (no table of its own): main's latest move under one key, each lane's last-seen mark under another.</summary>
/// <param name="values">The key-value store.</param>
internal sealed class SqliteBoardStore(IKeyValueStore values) : IBoardStore
{
    public async ValueTask<Result<Unit>> RecordMain(string sha, IReadOnlyList<int> mergedPrs, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mergedPrs);
        var current = await Read(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        // The same sha again (merge-run, then the watch seeing main move) is not a new move: it keeps the version, so no lane sees a second line, and joins the pull requests.
        var facts = current.Value is { } board && string.Equals(board.MainSha, sha, StringComparison.Ordinal)
            ? board with { MergedPrs = [.. board.MergedPrs.Union(mergedPrs)] }
            : new BoardFacts(sha, mergedPrs, at, (current.Value?.Version ?? 0) + 1);
        var prs = string.Join(',', facts.MergedPrs.Select(pr => pr.ToString(CultureInfo.InvariantCulture)));
        var text = string.Create(CultureInfo.InvariantCulture, $"{facts.Version}|{facts.MainSha}|{facts.At.UtcTicks}|{prs}");
        return await values.Set(KvKeys.BoardMain, text, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<BoardFacts?>> Read(CancellationToken cancellationToken)
    {
        var stored = await values.Get(KvKeys.BoardMain, cancellationToken).ConfigureAwait(false);
        return stored.IsError ? stored.Error : Results.Success(Parse(stored.Value));
    }

    public async ValueTask<Result<BoardSeen?>> SeenVersion(string lane, CancellationToken cancellationToken)
    {
        var stored = await values.Get(KvKeys.BoardSeenPrefix + lane, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var parts = stored.Value?.Split(',');
        return Results.Success(
            parts is { Length: 2 }
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var board)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var map)
                ? new BoardSeen(board, map)
                : null);
    }

    public ValueTask<Result<Unit>> MarkSeen(string lane, BoardSeen seen, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seen);
        return values.Set(KvKeys.BoardSeenPrefix + lane, string.Create(CultureInfo.InvariantCulture, $"{seen.BoardVersion},{seen.LaneMapVersion}"), cancellationToken);
    }

    // A value that doesn't parse reads as no move recorded: the next move replaces it.
    private static BoardFacts? Parse(string? stored)
    {
        var parts = stored?.Split('|');
        if (parts is not { Length: 4 }
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return null;
        }

        // A bad entry (a hand edit, a half write) is skipped, not an error.
        var prs = parts[3].Split(',').Where(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)).Select(p => int.Parse(p, NumberStyles.None, CultureInfo.InvariantCulture)).ToList();
        return new BoardFacts(parts[1], prs, new DateTimeOffset(ticks, TimeSpan.Zero), version);
    }
}
