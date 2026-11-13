using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
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
internal sealed partial class WatchLoop(TeamConfig team, WatchPoller poller, Func<string, ValueTask> emit, WatchResources reported, Func<WatchResources, ValueTask> remember, ContextReported context, Func<ValueTask<IReadOnlyList<string>>> usage)
{
    /// <summary>Called with main's new sha and the pull requests that went MERGED in the same poll, when a poll reports "MAIN moved".</summary>
    internal Func<string, IReadOnlyList<int>, ValueTask<Result<Unit>>> MainMoved { get; init; } = static (_, _) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

    /// <summary>Records main as the board's first move when the board holds none yet (the watch's start).</summary>
    internal Func<string, ValueTask<Result<Unit>>> SeedMain { get; init; } = static _ => ValueTask.FromResult<Result<Unit>>(Unit.Value);

    /// <summary>
    /// Keeps the pull requests and main's sha after each good poll, so the next run diffs its first poll against them.
    /// Returns the pid of the newer watch when another watch wrote the snapshot meanwhile, and null when it kept it.
    /// </summary>
    internal Func<WatchState, ValueTask<string?>> KeepSnapshot { get; init; } = static _ => ValueTask.FromResult<string?>(null);

    /// <summary>Returns the pid of a newer watch that claimed the team's watch, or null while this watch still owns it.</summary>
    internal Func<ValueTask<string?>> NewerOwner { get; init; } = static () => ValueTask.FromResult<string?>(null);

    /// <summary>The pid of the newer watch that ended this one, or null while this watch is still the newest. A superseded watch writes nothing more.</summary>
    internal string? Superseded { get; private set; }

    /// <summary>
    /// Keeps whether polls are failing, whenever that changes (a failing streak spans runs).
    /// Returns the pid of the newer watch when another watch wrote the flag meanwhile, and null when it kept it.
    /// </summary>
    internal Func<bool, ValueTask<string?>> KeepFailing { get; init; } = static _ => ValueTask.FromResult<string?>(null);

    /// <summary>Receives context.high and context.ok; null writes none.</summary>
    internal Core.Coordination.MetaEventSink? EventSink { get; init; }

    /// <summary>Dates the events.</summary>
    internal TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>The lanes' live sessions for LANE DOWN / LANE UP (#2856); null leaves them out.</summary>
    internal LaneSessions? Sessions { get; init; }

    /// <summary>Batches the status lines (#2651).</summary>
    internal required StatusBatch Status { get; init; }

    /// <summary>Report every pull request, not only the team's.</summary>
    internal bool AllPrs { get; init; }

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
        var polled = await poller.Poll(team, AllPrs, cancellationToken).ConfigureAwait(false);
        if (await Supersede(await NewerOwner().ConfigureAwait(false)).ConfigureAwait(false))
        {
            return;
        }

        if (polled.IsError)
        {
            await Failed(polled.Error).ConfigureAwait(false);
            return;
        }

        if (await Recovered().ConfigureAwait(false))
        {
            return;
        }

        var step = await SayPoll(polled.Value).ConfigureAwait(false);

        (_inbox, Open) = (polled.Value.Inbox, polled.Value.Open);
        _state = step.State;
        if (await Supersede(await KeepSnapshot(_state).ConfigureAwait(false)).ConfigureAwait(false))
        {
            return;
        }

        await Remember().ConfigureAwait(false);
    }

    // The lines one good poll yields: the start line (once), the step, the worktrees, new inbox messages, then the lanes' standing.
    private async ValueTask<WatchStep> SayPoll(Polled poll)
    {
        var step = WatchDiff.Step(_state, poll.Snapshot);
        if (!_started)
        {
            _started = true;
            await Say(Started(poll.Snapshot)).ConfigureAwait(false);
            await SeedBoard(poll.Snapshot).ConfigureAwait(false);
        }

        await SayStep(step, poll.Snapshot).ConfigureAwait(false);
        foreach (var line in poll.Queue)
        {
            await Say(line).ConfigureAwait(false);
        }

        foreach (var line in poll.Worktrees)
        {
            await Say(line).ConfigureAwait(false);
        }

        await SayInbox(poll).ConfigureAwait(false);

        await ReportLanes(poll.Context).ConfigureAwait(false);

        return step;
    }

    // New mail is an INBOX line; status lines ride on it or print batched (StatusBatch).
    private async ValueTask SayInbox(Polled poll)
    {
        var mail = _inbox is { } before && poll.Inbox > before ? string.Create(CultureInfo.InvariantCulture, $"INBOX {poll.Inbox - before} new message(s)") : null;
        if (await Status.Line(poll.Status, mail).ConfigureAwait(false) is { } line)
        {
            await Say(line).ConfigureAwait(false);
        }
    }

    // A newer watch owns the team's watch keys: say so once and stop writing them.
    private async ValueTask<bool> Supersede(string? newer)
    {
        if (newer is null)
        {
            return false;
        }

        Superseded = newer;
        await Say($"WATCH superseded by pid {newer}: a newer watch runs, do not restart").ConfigureAwait(false);
        return true;
    }

    // The first failed poll of a streak (which can span runs) says WATCH ERROR; the rest say nothing.
    private async ValueTask Failed(Error error)
    {
        if (_failing)
        {
            return;
        }

        _failing = true;
        if (await Supersede(await KeepFailing(true).ConfigureAwait(false)).ConfigureAwait(false))
        {
            return;
        }

        await Say($"WATCH ERROR: {error.Code}: {error.Message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
    }

    // A good poll ends the failing streak. True when keeping that showed a newer watch (this one is then superseded).
    private async ValueTask<bool> Recovered()
    {
        if (!_failing)
        {
            return false;
        }

        _failing = false;
        return await Supersede(await KeepFailing(false).ConfigureAwait(false)).ConfigureAwait(false);
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
