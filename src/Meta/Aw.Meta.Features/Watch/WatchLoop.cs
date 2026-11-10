using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>The state of one watch across its polls, and the lines each poll yields. Not shared between watches.</summary>
/// <param name="team">The team polled: its repo and its disk and memory floors.</param>
/// <param name="poller">Takes the snapshots.</param>
/// <param name="emit">Receives each line.</param>
/// <param name="reported">The disk and memory states an earlier watch reported (a re-armed watch does not announce them again).</param>
/// <param name="remember">Keeps the reported states whenever they change.</param>
/// <param name="context">The lanes an earlier watch reported CONTEXT HIGH for (a re-armed watch does not announce them again), and how to keep them.</param>
/// <param name="usage">Looks at the lanes and the meta for usage limits and returns the lines to print (USAGE LIMITED / USAGE OK).</param>
internal sealed class WatchLoop(TeamConfig team, WatchPoller poller, Func<string, ValueTask> emit, WatchResources reported, Func<WatchResources, ValueTask> remember, ContextReported context, Func<ValueTask<IReadOnlyList<string>>> usage)
{
    /// <summary>Called with main's new sha and the pull requests that went MERGED in the same poll, when a poll reports "MAIN moved".</summary>
    internal Func<string, IReadOnlyList<int>, ValueTask<Result<Unit>>> MainMoved { get; init; } = static (_, _) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

    /// <summary>Records main as the board's first move when the board holds none yet (the watch's start).</summary>
    internal Func<string, ValueTask<Result<Unit>>> SeedMain { get; init; } = static _ => ValueTask.FromResult<Result<Unit>>(Unit.Value);

    /// <summary>Keeps the pull requests and main's sha after each good poll, so the next run diffs its first poll against them.</summary>
    internal Func<WatchState, ValueTask> KeepSnapshot { get; init; } = static _ => ValueTask.CompletedTask;

    /// <summary>Keeps whether polls are failing, whenever that changes (a failing streak spans runs).</summary>
    internal Func<bool, ValueTask> KeepFailing { get; init; } = static _ => ValueTask.CompletedTask;

    /// <summary>The state the first poll diffs against: the last poll of an earlier run, or the state before any poll.</summary>
    internal WatchState Baseline { init => _state = value; }

    /// <summary>Whether the earlier run ended on a failing poll: the streak's next errors are not events.</summary>
    internal bool Failing { init => _failing = value; }

    private WatchState _state = WatchState.Continuing(reported);
    private WatchResources _kept = reported;
    private int? _inbox;
    private bool _failing;
    private bool _started;
    private IReadOnlySet<string> _high = context.High;

    /// <summary>How many inbox messages were still owed a reply at the last poll.</summary>
    internal int Open { get; private set; }

    /// <summary>How many lines were emitted.</summary>
    internal int Lines { get; private set; }

    /// <summary>Takes a snapshot and emits what changed. A failed poll is reported once per streak.</summary>
    internal async ValueTask Tick(CancellationToken cancellationToken)
    {
        var polled = await poller.Poll(team, cancellationToken).ConfigureAwait(false);
        if (polled.IsError)
        {
            await Failed(polled.Error).ConfigureAwait(false);
            return;
        }

        await Recovered().ConfigureAwait(false);

        var step = WatchDiff.Step(_state, polled.Value.Snapshot);
        if (!_started)
        {
            _started = true;
            await Say(Started(polled.Value.Snapshot)).ConfigureAwait(false);
            await SeedBoard(polled.Value.Snapshot).ConfigureAwait(false);
        }

        await SayStep(step, polled.Value.Snapshot).ConfigureAwait(false);
        foreach (var line in polled.Value.Worktrees)
        {
            await Say(line).ConfigureAwait(false);
        }

        if (_inbox is { } before && polled.Value.Inbox > before)
        {
            await Say(string.Create(CultureInfo.InvariantCulture, $"INBOX {polled.Value.Inbox - before} new message(s)")).ConfigureAwait(false);
        }

        await ReportLanes(polled.Value.Context).ConfigureAwait(false);

        (_inbox, Open) = (polled.Value.Inbox, polled.Value.Open);
        _state = step.State;
        await KeepSnapshot(_state).ConfigureAwait(false);
        await Remember().ConfigureAwait(false);
    }

    // The first failed poll of a streak (which can span runs) says WATCH ERROR; the rest say nothing.
    private async ValueTask Failed(Error error)
    {
        if (_failing)
        {
            return;
        }

        _failing = true;
        await KeepFailing(true).ConfigureAwait(false);
        await Say($"WATCH ERROR: {error.Code}: {error.Message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
    }

    private async ValueTask Recovered()
    {
        if (_failing)
        {
            _failing = false;
            await KeepFailing(false).ConfigureAwait(false);
        }
    }

    // The lanes' standing: context sizes, then usage limits. Neither can fail the poll.
    private async ValueTask ReportLanes(IReadOnlyList<LaneContext>? contexts)
    {
        await ReportContext(contexts).ConfigureAwait(false);
        foreach (var line in await usage().ConfigureAwait(false))
        {
            await Say(line).ConfigureAwait(false);
        }
    }

    // CONTEXT HIGH (META RECYCLE for the meta lane) once per crossing, CONTEXT OK after a recycle; the lanes reported high are kept for a re-armed watch.
    private async ValueTask ReportContext(IReadOnlyList<LaneContext>? contexts)
    {
        if (contexts is null)
        {
            return;
        }

        var (lines, high) = ContextWatch.Step(_high, contexts, team.MetaLane);
        foreach (var line in lines)
        {
            await Say(line).ConfigureAwait(false);
        }

        if (!high.SetEquals(_high))
        {
            _high = high;
            await context.Keep(high).ConfigureAwait(false);
        }
    }

    // Keeps the disk and memory states reported so far, whenever they change.
    private async ValueTask Remember()
    {
        var now = new WatchResources(_state.DiskLow, _state.MemoryLow);
        if (now != _kept)
        {
            _kept = now;
            await remember(now).ConfigureAwait(false);
        }
    }

    private async ValueTask SayStep(WatchStep step, WatchSnapshot snapshot)
    {
        foreach (var line in step.Lines)
        {
            await Say(line).ConfigureAwait(false);
        }

        await RecordMain(step, snapshot).ConfigureAwait(false);
    }

    // The board: a poll that reports "MAIN moved" records main's sha and the pull requests that went MERGED since the last poll.
    private async ValueTask RecordMain(WatchStep step, WatchSnapshot snapshot)
    {
        if (!step.Lines.Any(l => l.StartsWith("MAIN moved", StringComparison.Ordinal)))
        {
            return;
        }

        var merged = snapshot.Prs
            .Where(pr => pr.State == PrState.Merged && _state.Prs.TryGetValue(pr.Number, out var known) && known.State != PrState.Merged)
            .Select(pr => pr.Number)
            .Order()
            .ToList();
        await SayIfFailed(await MainMoved(snapshot.MainSha, merged).ConfigureAwait(false)).ConfigureAwait(false);
    }

    // A fresh install's board holds main as the watch found it. A board that already holds a move (a restart, or merge-run's newer record) is left alone.
    private async ValueTask SeedBoard(WatchSnapshot snapshot)
    {
        if (snapshot.MainSha.Length > 0)
        {
            await SayIfFailed(await SeedMain(snapshot.MainSha).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }

    private async ValueTask SayIfFailed(Result<Unit> written)
    {
        if (written.IsError)
        {
            await Say($"WATCH ERROR: {written.Error.Code}: {written.Error.Message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
        }
    }

    /// <summary>How many lines were emitted that report something (every line but the start line).</summary>
    internal int Events { get; private set; }

    /// <summary>Emits one line.</summary>
    internal async ValueTask Say(string line)
    {
        Lines++;
        if (!line.StartsWith("WATCH started", StringComparison.Ordinal))
        {
            Events++;
        }

        await emit(line).ConfigureAwait(false);
    }

    private static string Started(WatchSnapshot snapshot) =>
        string.Create(CultureInfo.InvariantCulture, $"WATCH started: {snapshot.Prs.Count(pr => pr.State == PrState.Open)} open PR(s), main {(snapshot.MainSha.Length > 7 ? snapshot.MainSha[..7] : snapshot.MainSha)}");
}
