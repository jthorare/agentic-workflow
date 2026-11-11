using System.Globalization;
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
        new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, Worlds.FollowUps(_world), Worlds.Reservation(_world), _world.Context),
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

    private static string Pr(int number, string state, string head) =>
        string.Create(CultureInfo.InvariantCulture, $$"""{"number":{{number}},"state":"{{state}}","isDraft":false,"headRefOid":"{{head}}","mergeable":"MERGEABLE","title":"t{{number}}"}""");

    // Poll n (from 0, across runs) sees lists[n] (the last repeats) and mains[n]; a "fail" list makes gh fail.
    private void ScriptLists(string[] lists, string[] mains, Action<int>? onPoll = null)
    {
        var polls = -1;
        _ = _world.Runner
            .When("gh pr list", _ =>
            {
                polls++;
                onPoll?.Invoke(polls);
                var list = lists[Math.Min(polls, lists.Length - 1)];
                return string.Equals(list, "fail", StringComparison.Ordinal)
                    ? new CommandResult(1, string.Empty, "network down", TimedOut: false)
                    : new CommandResult(0, list, string.Empty, TimedOut: false);
            })
            .When("git ls-remote origin main", _ => new CommandResult(0, mains[Math.Min(Math.Max(polls, 0), mains.Length - 1)] + "\trefs/heads/main\n", string.Empty, TimedOut: false));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_change_between_two_runs_is_reported_by_the_next_runs_first_poll()
    {
        ScriptLists(
            [
                "[" + Pr(190, "OPEN", "aaa1111") + "]",
                "[" + Pr(190, "MERGED", "aaa1111") + "," + Pr(191, "OPEN", "bbb2222") + "]",
            ],
            ["aaaaaaa1111111", "bbbbbbb2222222"]);
        await _world.Ready();

        // Run 1 is a plain watch whose window holds one poll; it ends on its WATCH EXPIRING line.
        var first = await Service().Handle(new WatchRequest(_interval, _interval, line => { _lines.Add(line); return ValueTask.CompletedTask; }), CancellationToken.None);
        Assert.True(first.IsSuccess);
        _lines.Clear();

        var second = await Service().Handle(Request(untilEvent: true), CancellationToken.None);

        Assert.True(second.IsSuccess, second.IsError ? second.Error.Message : string.Empty);
        Assert.Equal(1, second.Value.Polls);
        Assert.Contains("PR #190 merged", _lines, StringComparer.Ordinal);
        Assert.Contains("PR #191 opened", _lines, StringComparer.Ordinal);
        Assert.Contains("MAIN moved bbbbbbb", _lines, StringComparer.Ordinal);
        var board = (await _world.Board.Read(CancellationToken.None)).Value;
        Assert.NotNull(board);
        Assert.Equal("bbbbbbb2222222", board.MainSha);
        Assert.Equal([190], board.MergedPrs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_poller_ends_one_until_event_run_and_the_next_run_stays_quiet()
    {
        using var stop = new CancellationTokenSource();
        ScriptLists(["fail"], ["aaaaaaa1111111"], onPoll: n =>
        {
            if (n == 6)
            {
                stop.Cancel();
            }
        });
        await _world.Ready();

        var first = await Service().Handle(Request(untilEvent: true), stop.Token);
        Assert.True(first.IsSuccess);
        _ = Assert.Single(_lines, l => l.StartsWith("WATCH ERROR", StringComparison.Ordinal));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Service().Handle(Request(untilEvent: true), stop.Token));

        _ = Assert.Single(_lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_good_poll_ends_the_failing_streak_so_the_next_failure_is_reported_again()
    {
        ScriptLists(["fail", "[]", "fail"], ["aaaaaaa1111111"]);
        await _world.Ready();

        Assert.True((await Service().Handle(Request(untilEvent: true), CancellationToken.None)).IsSuccess);   // poll 0 fails
        Assert.True((await Service().Handle(new WatchRequest(_interval, _interval, line => { _lines.Add(line); return ValueTask.CompletedTask; }), CancellationToken.None)).IsSuccess);   // poll 1 is good
        _lines.Clear();
        Assert.True((await Service().Handle(Request(untilEvent: true), CancellationToken.None)).IsSuccess);   // poll 2 fails again

        _ = Assert.Single(_lines, l => l.StartsWith("WATCH ERROR", StringComparison.Ordinal));
    }
}
