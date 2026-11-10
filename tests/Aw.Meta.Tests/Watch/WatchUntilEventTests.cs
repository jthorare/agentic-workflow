using Aw.Meta.Core.Commands;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

/// <summary>#2498: a Claude meta runs watch as a background Bash command, which wakes the model only when it exits. --until-event has no window and no WATCH EXPIRING: it exits on the first real line.</summary>
public sealed class WatchUntilEventTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(120);
    private readonly MetaWorld _world = new();
    private readonly List<string> _lines = [];

    private WatchService Service() => new(
        _world.Config,
        new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, new WatchSweeper(_world.Config, _world.Sweeper, _world.Values), Worlds.Reservation(_world), _world.Context),
        _world.Values,
        _world.Delay,
        _world.Clock,
        _world.Monitor,
        _world.Board);

    private WatchRequest Request(bool untilEvent) =>
        new(_interval, TimeSpan.FromMinutes(10), line => { _lines.Add(line); return ValueTask.CompletedTask; }) { UntilEvent = untilEvent };

    // Poll n (from 0) lists a pull request from poll `prAtPoll` on; `onPoll` sees each poll number.
    private void Script(int prAtPoll, Action<int>? onPoll = null)
    {
        var polls = -1;
        _ = _world.Runner
            .When("gh pr list", _ =>
            {
                polls++;
                onPoll?.Invoke(polls);
                return new CommandResult(0, polls >= prAtPoll ? """[{"number":190,"state":"OPEN","isDraft":false,"headRefOid":"aaa1111","mergeable":"MERGEABLE","title":"t190"}]""" : "[]", string.Empty, TimedOut: false);
            })
            .When("git ls-remote origin main", _ => new CommandResult(0, "aaaaaaa1111111\trefs/heads/main\n", string.Empty, TimedOut: false));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_quiet_window_end_keeps_watching_until_a_real_line()
    {
        using var stop = new CancellationTokenSource();
        var polls = 0;
        Script(prAtPoll: int.MaxValue, onPoll: n =>
        {
            polls = n + 1;
            if (polls == 12)
            {
                stop.Cancel();
            }
        });
        await _world.Ready();

        // The 10 minute window holds 5 polls; the watch must still be polling at poll 12.
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Service().Handle(Request(untilEvent: true), stop.Token));

        Assert.True(polls >= 12);
        Assert.DoesNotContain(_lines, l => l.StartsWith("WATCH EXPIRING", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_real_line_is_printed_and_the_call_returns()
    {
        Script(prAtPoll: 8);
        await _world.Ready();

        var result = await Service().Handle(Request(untilEvent: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(9, result.Value.Polls);
        Assert.Contains("#190", _lines[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(_lines, l => l.StartsWith("WATCH EXPIRING", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Plain_watch_still_prints_WATCH_EXPIRING_at_its_window_end()
    {
        Script(prAtPoll: int.MaxValue);
        await _world.Ready();

        var result = await Service().Handle(Request(untilEvent: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("WATCH EXPIRING: rerun aw-meta watch", _lines[^1]);
    }
}
