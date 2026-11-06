using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Usage;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

public sealed class WatchTests
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
        new UsageMonitor(new UsageReader(_world.LaneFiles, _world.LaneSessions, _world.ClaudeTranscripts, _world.Panes, _world.Messages, _world.Clock), new UsageWatcher(new PaneSubmitter(_world.Panes, _world.Delay), _world.Log, _world.Values, _world.Clock)));

    private static string Pr(int number, string state, string head, string mergeable = "MERGEABLE", bool draft = false) =>
        string.Create(CultureInfo.InvariantCulture, $$"""{"number":{{number}},"state":"{{state}}","isDraft":{{(draft ? "true" : "false")}},"headRefOid":"{{head}}","mergeable":"{{mergeable}}","title":"t{{number}}"}""");

    private static string List(params string[] prs) => "[" + string.Join(',', prs) + "]";

    // The poll n (from 0) sees lists[n] (the last one repeats) and mains[n].
    private void Script(string[] lists, string[]? mains = null, Action<int>? onPoll = null)
    {
        var polls = -1;
        var main = mains ?? ["aaaaaaa1111111"];
        _ = _world.Runner
            .When("gh pr list", _ =>
            {
                polls++;
                onPoll?.Invoke(polls);
                return lists[Math.Min(polls, lists.Length - 1)].StartsWith("fail", StringComparison.Ordinal)
                    ? new CommandResult(1, string.Empty, "network down", TimedOut: false)
                    : new CommandResult(0, lists[Math.Min(polls, lists.Length - 1)], string.Empty, TimedOut: false);
            })
            .When("git ls-remote origin main", _ => new CommandResult(0, main[Math.Min(Math.Max(polls, 0), main.Length - 1)] + "\trefs/heads/main\n", string.Empty, TimedOut: false));
    }

    private async Task<WatchResponse> Watch(TimeSpan duration)
    {
        var result = await Service().Handle(new WatchRequest(_interval, duration, line => { _lines.Add(line); return ValueTask.CompletedTask; }), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_settled_change_is_one_line_and_nothing_is_reprinted()
    {
        await _world.Ready();
        Script(
            [
                List(Pr(190, "OPEN", "aaa1111")),
                List(Pr(190, "OPEN", "aaa1111"), Pr(191, "OPEN", "bbb2222", "UNKNOWN")),
                List(Pr(190, "OPEN", "ccc3333", "CONFLICTING"), Pr(191, "OPEN", "bbb2222", "UNKNOWN")),
                List(Pr(190, "OPEN", "ccc3333", "CONFLICTING"), Pr(191, "MERGED", "bbb2222", "UNKNOWN")),
                List(Pr(190, "OPEN", "ccc3333", "CONFLICTING"), Pr(191, "MERGED", "bbb2222", "UNKNOWN")),
            ],
            mains: ["aaaaaaa1111111", "aaaaaaa1111111", "bbbbbbb2222222"]);

        var response = await Watch(TimeSpan.FromSeconds(540));

        Assert.Equal(
            [
                "WATCH started: 1 open PR(s), main aaaaaaa",
                "PR #191 opened",
                "PR #190 head ccc3333",
                "MAIN moved bbbbbbb",
                "PR #190 mergeable CONFLICTING",
                "PR #191 merged",
                "WATCH EXPIRING: rerun aw-meta watch",
            ],
            _lines);
        Assert.Equal((5, 7), (response.Polls, response.Lines));
        Assert.DoesNotContain(_lines, l => l.Contains("UNKNOWN", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task It_waits_the_interval_between_polls_and_expires_before_the_duration_ends()
    {
        await _world.Ready();
        Script([List(Pr(190, "OPEN", "aaa1111"))]);

        var response = await Watch(TimeSpan.FromSeconds(360));

        Assert.Equal(3, response.Polls);
        Assert.Equal([_interval, _interval], _world.Delay.Waits);
        Assert.Equal("WATCH EXPIRING: rerun aw-meta watch", _lines[^1]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_poll_writes_the_heartbeat_and_the_interval()
    {
        await _world.Ready();
        Script([List(Pr(190, "OPEN", "aaa1111"))]);
        var start = _world.Clock.GetUtcNow();

        _ = await Watch(TimeSpan.FromSeconds(360));

        Assert.Equal(start.AddSeconds(240).ToString("O"), (await _world.Values.Get(KvKeys.WatchHeartbeat, CancellationToken.None)).Value);
        Assert.Equal("120", (await _world.Values.Get(KvKeys.WatchInterval, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pull_requests_seen_merged_or_closed_are_kept_for_the_merge_queue_to_drop()
    {
        await _world.Ready();
        _ = await _world.Values.Set(KvKeys.EndedPrs, "180", CancellationToken.None);
        Script([List(Pr(190, "OPEN", "aaa1111"), Pr(191, "MERGED", "bbb2222"), Pr(192, "CLOSED", "ccc3333"))]);

        _ = await Watch(TimeSpan.FromSeconds(120));

        Assert.Equal("180,191,192", (await _world.Values.Get(KvKeys.EndedPrs, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_poll_reports_once_per_streak_and_the_watch_carries_on()
    {
        await _world.Ready();
        Script([List(Pr(190, "OPEN", "aaa1111")), "fail", "fail", List(Pr(190, "OPEN", "aaa1111"))]);

        var response = await Watch(TimeSpan.FromSeconds(600));

        Assert.Equal(3, _lines.Count);
        Assert.StartsWith("WATCH ERROR: AW-META-012", _lines[1], StringComparison.Ordinal);
        Assert.Equal(5, response.Polls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Low_disk_and_its_recovery_are_reported_once_each()
    {
        await _world.Ready();
        _world.Resources.Resources = new HostResources(16_000_000, 8_000_000, 0, DiskFreeBytes: 1_000_000_000);
        Script([List(Pr(190, "OPEN", "aaa1111"))], onPoll: poll =>
        {
            if (poll == 2)
            {
                _world.Resources.Resources = new HostResources(16_000_000, 8_000_000, 0, DiskFreeBytes: 80_000_000_000);
            }
        });

        _ = await Watch(TimeSpan.FromSeconds(600));

        Assert.Equal(["DISK LOW", "DISK OK"], _lines.Where(l => l.StartsWith("DISK", StringComparison.Ordinal)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Low_memory_and_its_recovery_are_reported_once_each()
    {
        await _world.Ready();
        _world.Resources.Resources = new HostResources(64_000_000, 4_000_000, 0, DiskFreeBytes: 80_000_000_000);
        Script([List(Pr(190, "OPEN", "aaa1111"))], onPoll: poll =>
        {
            if (poll == 2)
            {
                _world.Resources.Resources = new HostResources(64_000_000, 20_000_000, 0, DiskFreeBytes: 80_000_000_000);
            }
        });

        _ = await Watch(TimeSpan.FromSeconds(600));

        Assert.Equal(["MEMORY LOW", "MEMORY OK"], _lines.Where(l => l.StartsWith("MEMORY", StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.DoesNotContain(_lines, l => l.StartsWith("DISK", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_re_armed_watch_does_not_announce_a_low_state_the_last_one_reported()
    {
        await _world.Ready();
        _world.Resources.Resources = new HostResources(64_000_000, 4_000_000, 0, DiskFreeBytes: 80_000_000_000);
        Script([List(Pr(190, "OPEN", "aaa1111"))]);

        _ = await Watch(TimeSpan.FromSeconds(240));
        var first = _lines.Count(l => l.StartsWith("MEMORY", StringComparison.Ordinal));
        _lines.Clear();
        _ = await Watch(TimeSpan.FromSeconds(240));

        Assert.Equal(1, first);
        Assert.DoesNotContain(_lines, l => l.StartsWith("MEMORY", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_re_armed_watch_reports_the_recovery_of_a_state_the_last_one_reported_low()
    {
        await _world.Ready();
        _ = await _world.Values.Set(KvKeys.WatchResources, "disk=ok mem=low", CancellationToken.None);
        _world.Resources.Resources = new HostResources(64_000_000, 20_000_000, 0, DiskFreeBytes: 80_000_000_000);
        Script([List(Pr(190, "OPEN", "aaa1111"))]);

        _ = await Watch(TimeSpan.FromSeconds(240));

        Assert.Equal(["MEMORY OK"], _lines.Where(l => l.StartsWith("MEMORY", StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.Equal("disk=ok mem=ok", (await _world.Values.Get(KvKeys.WatchResources, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pull_requests_seen_ending_lower_the_reservation()
    {
        await _world.Ready();
        for (var n = 0; n < 7; n++)
        {
            await _world.Receive("m" + n.ToString(CultureInfo.InvariantCulture), "t1-api", string.Create(CultureInfo.InvariantCulture, $"ready #{190 + n} 079c2541"));
        }

        Script([List(Pr(190, "OPEN", "aaa1111")), List(Pr(190, "OPEN", "aaa1111"), Pr(191, "MERGED", "b"), Pr(192, "CLOSED", "c"), Pr(193, "MERGED", "d"), Pr(194, "CLOSED", "e"), Pr(195, "MERGED", "f"))]);

        _ = await Watch(TimeSpan.FromSeconds(120));
        var before = _world.Heavy.Reserve;
        _lines.Clear();
        _ = await Watch(TimeSpan.FromSeconds(120));

        Assert.Equal(2, before);   // depth 7 holds the cap less one (#2100)
        Assert.Equal(0, _world.Heavy.Reserve);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_poll_goes_on_when_the_reservation_cannot_be_kept()
    {
        await _world.Ready();
        for (var n = 0; n < 3; n++)
        {
            await _world.Receive("m" + n.ToString(CultureInfo.InvariantCulture), "t1-api", string.Create(CultureInfo.InvariantCulture, $"ready #{190 + n} 079c2541"));
        }

        _world.Heavy.FailWrites = true;   // depth 3 wants one slot, so the write is reached
        Script([List(Pr(190, "OPEN", "aaa1111")), List(Pr(190, "OPEN", "aaa1111"), Pr(191, "OPEN", "bbb2222"))]);

        _ = await Watch(TimeSpan.FromSeconds(240));

        Assert.Equal(0, _world.Heavy.Writes);
        Assert.Contains(_lines, l => l.Contains("#191", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nearly_full_swap_is_not_low_memory_while_ram_is_above_the_floor()
    {
        await _world.Ready();
        _world.Resources.Resources = new HostResources(64_000_000, 40_000_000, SwapUsedKb: 9_500_000, DiskFreeBytes: 80_000_000_000, SwapTotalKb: 10_000_000);
        Script([List(Pr(190, "OPEN", "aaa1111"))]);

        _ = await Watch(TimeSpan.FromSeconds(240));

        Assert.DoesNotContain(_lines, l => l.StartsWith("MEMORY", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_floors_come_from_the_team_config()
    {
        _ = await _world.Config.Save(
            new Core.Config.TeamConfig("t1", "/work", ["meta", "api"]) { Resources = new Core.Config.ResourcesConfig(DiskFloorGb: 200, MemFloorGb: 40, SwapHighPct: 90) },
            CancellationToken.None);
        _world.Resources.Resources = new HostResources(64_000_000, 20_000_000, 0, DiskFreeBytes: 80_000_000_000);
        Script([List(Pr(190, "OPEN", "aaa1111"))]);

        _ = await Watch(TimeSpan.FromSeconds(240));

        Assert.Equal(["DISK LOW", "MEMORY LOW"], _lines.Where(l => l.StartsWith("DISK", StringComparison.Ordinal) || l.StartsWith("MEMORY", StringComparison.Ordinal)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task New_inbox_messages_between_polls_are_reported()
    {
        await _world.Ready();
        Script([List(Pr(190, "OPEN", "aaa1111"))], onPoll: poll =>
        {
            if (poll == 1)
            {
                _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "ready #190 079c2541") + MetaWorld.PeerLine("m2", "t1-ui", "hello"));
                _ = _world.Sync.Run(MetaWorld.Transcript, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
        });

        _ = await Watch(TimeSpan.FromSeconds(360));

        Assert.Equal(["INBOX 2 new message(s)"], _lines.Where(l => l.StartsWith("INBOX", StringComparison.Ordinal)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_interval_and_duration_must_make_sense()
    {
        var validator = new WatchValidator();
        static ValueTask Ignore(string line) => ValueTask.CompletedTask;

        Assert.True(validator.Validate(new WatchRequest(_interval, TimeSpan.FromMinutes(29), Ignore)).IsSuccess);
        Assert.All(
            [new WatchRequest(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), Ignore), new WatchRequest(_interval, TimeSpan.FromSeconds(60), Ignore), new WatchRequest(_interval, TimeSpan.FromHours(30), Ignore)],
            request => Assert.Equal(MetaErrors.Invalid.Code, validator.Validate(request).Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_crossing_the_context_threshold_is_reported_once_and_ok_after_a_recycle_and_a_re_armed_watch_does_not_repeat_it()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new Core.Lanes.LaneRecord("api", Core.Ids.SessionId.From("s-old"), Agent: null, A.At(1)));
        _world.ClaudeTranscripts.PutUsage("s-old", 487_000, A.At(5));
        Script([List(Pr(190, "OPEN", "aaa1111"))], onPoll: poll =>
        {
            if (poll == 2)
            {
                _world.LaneSessions.Set("t1-api", "s-new");
                _world.ClaudeTranscripts.PutUsage("s-new", 9_000, A.At(9));
            }
        });

        _ = await Watch(TimeSpan.FromSeconds(540));
        var context = _lines.Where(l => l.StartsWith("CONTEXT", StringComparison.Ordinal)).ToList();

        Assert.Equal(["CONTEXT HIGH api 487k", "CONTEXT OK api"], context);
        _lines.Clear();
        _world.ClaudeTranscripts.PutUsage("s-new", 450_000, A.At(20));
        _ = await Watch(TimeSpan.FromSeconds(240));
        Assert.Equal(["CONTEXT HIGH api 450k"], _lines.Where(l => l.StartsWith("CONTEXT", StringComparison.Ordinal)), StringComparer.Ordinal);
        _lines.Clear();
        _ = await Watch(TimeSpan.FromSeconds(240));
        Assert.DoesNotContain(_lines, l => l.StartsWith("CONTEXT", StringComparison.Ordinal));
    }
}
