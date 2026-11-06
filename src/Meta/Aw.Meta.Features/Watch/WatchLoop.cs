using System.Globalization;
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
    private WatchState _state = WatchState.Continuing(reported);
    private WatchResources _kept = reported;
    private int? _inbox;
    private bool _failing;
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
            if (!_failing)
            {
                await Say($"WATCH ERROR: {polled.Error.Code}: {polled.Error.Message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
            }

            _failing = true;
            return;
        }

        _failing = false;
        var step = WatchDiff.Step(_state, polled.Value.Snapshot);
        if (!_state.Initialized)
        {
            await Say(Started(polled.Value.Snapshot)).ConfigureAwait(false);
        }

        foreach (var line in step.Lines)
        {
            await Say(line).ConfigureAwait(false);
        }

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
        await Remember().ConfigureAwait(false);
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

    // CONTEXT HIGH once per crossing, CONTEXT OK after a recycle; the lanes reported high are kept for a re-armed watch.
    private async ValueTask ReportContext(IReadOnlyList<LaneContext>? contexts)
    {
        if (contexts is null)
        {
            return;
        }

        var (lines, high) = ContextWatch.Step(_high, contexts);
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

    /// <summary>Emits one line.</summary>
    internal async ValueTask Say(string line)
    {
        Lines++;
        await emit(line).ConfigureAwait(false);
    }

    private static string Started(WatchSnapshot snapshot) =>
        string.Create(CultureInfo.InvariantCulture, $"WATCH started: {snapshot.Prs.Count(pr => pr.State == PrState.Open)} open PR(s), main {(snapshot.MainSha.Length > 7 ? snapshot.MainSha[..7] : snapshot.MainSha)}");
}
