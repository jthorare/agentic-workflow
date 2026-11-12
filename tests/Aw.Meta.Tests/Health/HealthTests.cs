using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.Health;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Health;

public sealed class HealthTests
{
    private const string Temporal = "temporal server start-dev --port 7233 --db-filename /tmp/aw-sindri-ab12/temporal.db";
    private const string McpServer = "node /x/fetch-mcp.js";
    private readonly MetaWorld _world = new();

    private HealthService Service() => new(
        new HostProbe(_world.Processes, _world.Resources, new OomPolicyCheck(_world.Runner, _world.Paths)),
        new LaneCheck(_world.Config, new MissingLanes(_world.Sessions, _world.Messages, _world.Lanes), _world.LaneHealth, _world.Context),
        new OrphanKiller(_world.Processes, _world.Log, _world.Clock),
        _world.Values,
        _world.Config,
        Worlds.Advisor(_world),
        _world.Clock);

    private async Task<HealthResponse> Check(bool kill = false)
    {
        var result = await Service().Handle(new HealthRequest(kill), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private void Orphan(int pid = 4242) => _world.Processes.Add(new ProcessInfo(pid, Ppid: 1, Temporal, RssKb: 204_800, AgeSeconds: 900));

    private void AllLanesLive()
    {
        _world.Sessions.Add(A.Live("t1-api", "s-api"));
        _world.Sessions.Add(A.Live("t1-ui", "s-ui"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Memory_by_group_and_the_host_figures_are_reported()
    {
        await _world.Ready();
        AllLanesLive();
        _world.Processes.Add(new ProcessInfo(10, 1, "claude --resume a", RssKb: 1_048_576, AgeSeconds: 5000));
        _world.Processes.Add(new ProcessInfo(11, 1, "dotnet build X.slnx", RssKb: 524_288, AgeSeconds: 60));
        _world.Resources.Resources = new HostResources(24_000_000, 12_000_000, 100_000, 50_000_000_000);

        var health = await Check();

        Assert.Equal([new ProcessGroup("claude", 1, 1024), new ProcessGroup("dotnet", 1, 512)], health.Groups);
        Assert.Equal((2, 50), (health.ProcessCount, health.Host.AvailablePercent));
        Assert.False(health.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_heavy_slots_the_reservation_and_the_builders_advice_are_reported()
    {
        await _world.Ready();
        AllLanesLive();
        _world.Heavy.Record = new HeavyReserveRecord(2, 5, A.At(50));
        _world.Resources.Resources = new HostResources(64_000_000, 16_000_000, 0, DiskFreeBytes: 132L * 1024 * 1024 * 1024);

        var health = await Check();

        Assert.Equal(new HeavySlots(3, 2, 5, A.At(50)), health.Heavy);
        Assert.Equal("heavy reservation: 2 of 3 slots (queue depth 5) - set it with meta-gate-reserve 5", health.Heavy!.Line());
        Assert.Equal("builders: 12 per lane (disk 132 GB free, mem 15 GB available)", health.Builders!.Line());
        Assert.False(health.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reservation_nobody_recomputed_for_half_an_hour_is_reported_stale()
    {
        await _world.Ready();
        AllLanesLive();
        _world.Heavy.Record = new HeavyReserveRecord(2, 5, A.At(20));

        var health = await Check();

        Assert.True(health.Heavy!.Stale);
        Assert.EndsWith("[stale: computed 04:20Z; run status or gate-run to recompute]", health.Heavy.Line(), StringComparison.Ordinal);
        Assert.Equal(_world.Heavy.Record, await KeptRecord());   // health only reads
    }

    private async Task<HeavyReserveRecord?> KeptRecord() => (await _world.Heavy.Read(CancellationToken.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task The_builders_advice_follows_the_team_configs_thresholds()
    {
        _ = await _world.Config.Save(
            new Core.Config.TeamConfig("t1", "/work", ["meta", "api", "ui"]) { Builders = new Core.Config.BuildersConfig(MaxPerLane: 3, WorktreeGb: 10) },
            CancellationToken.None);
        _world.Resources.Resources = new HostResources(64_000_000, 32_000_000, 0, DiskFreeBytes: 80L * 1024 * 1024 * 1024);

        var health = await Check();

        // meta is not a lane that builds: 2 lanes; 30 + 2 x 3 x 10 = 90 > 80, 30 + 2 x 2 x 10 = 70 fits
        Assert.Equal((2, 3), (health.Builders!.PerLane, health.Builders.MaxPerLane));
        Assert.Equal("builders: 2 per lane (disk below floor+headroom)", health.Builders.Line());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_the_advice_uses_the_defaults()
    {
        _world.Resources.Resources = new HostResources(64_000_000, 32_000_000, 0, DiskFreeBytes: 200L * 1024 * 1024 * 1024);

        var health = await Check();

        Assert.Equal(42, health.Builders!.PerLane);   // no cap: (200 GB - 30 GB floor) / (1 lane x 4 GB)
        Assert.Null(health.Heavy);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_orphan_is_reported_and_left_alone_without_the_flag()
    {
        await _world.Ready();
        AllLanesLive();
        Orphan();

        var health = await Check();

        Assert.Equal([4242], health.Orphans.Select(p => p.Pid));
        Assert.Empty(health.Killed);
        Assert.True(health.Attention);
        Assert.Contains((await _world.Processes.Snapshot(CancellationToken.None)).Value.Processes, p => p.Pid == 4242);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Killing_orphans_terminates_only_the_rule_matches_by_pid_and_logs_each_kill()
    {
        await _world.Ready();
        AllLanesLive();
        Orphan(4242);
        _world.Processes.Add(new ProcessInfo(4243, Ppid: 1, Temporal, RssKb: 1, AgeSeconds: 30));   // too young
        _world.Processes.Add(new ProcessInfo(4244, Ppid: 1, "temporal server start-dev --db-filename /home/me/dev.db", RssKb: 1, AgeSeconds: 900));   // not scratch
        _world.Processes.Add(new ProcessInfo(4245, Ppid: 1, "claude", RssKb: 1, AgeSeconds: 9000));

        var health = await Check(kill: true);

        Assert.Equal([4242], health.Killed);
        Assert.Equal([4243, 4244, 4245], (await _world.Processes.Snapshot(CancellationToken.None)).Value.Processes.Select(p => p.Pid));
        Assert.Equal("- INCIDENT 2026-10-15 05:00: health --kill-orphans sent SIGTERM to orphaned temporal dev server pid 4242 (up 15 min)", Assert.Single(_world.Log.Lines));
        Assert.False(health.Attention);   // the orphan is gone
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Full_swap_is_reported_as_a_figure_but_is_not_low_memory()
    {
        await _world.Ready();
        AllLanesLive();
        _world.Resources.Resources = new HostResources(32_000_000, 14_000_000, SwapUsedKb: 8_000_000, DiskFreeBytes: 50_000_000_000, SwapTotalKb: 8_000_000);

        var health = await Check();

        Assert.Equal((8_000_000L, 100), (health.Host.SwapUsedKb, health.Host.SwapUsedPercent));
        Assert.False(health.MemoryLow);
        Assert.False(health.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Low_memory_needs_attention()
    {
        await _world.Ready();
        AllLanesLive();
        _world.Resources.Resources = new HostResources(16_000_000, 1_000_000, 4_000_000, 50_000_000_000);

        var health = await Check();

        Assert.True(health.MemoryLow);
        Assert.True(health.Attention);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(7_000_000, true)]    // 7 GB available of 64: under the 8 GB floor, though over a tenth of the total
    [InlineData(9_000_000, false)]
    public async Task Memory_is_low_by_the_same_floor_the_watch_uses(long availableKb, bool low)
    {
        await _world.Ready();
        AllLanesLive();
        _world.Resources.Resources = new HostResources(64_000_000, availableKb, 0, 50_000_000_000);

        var health = await Check();

        Assert.Equal(low, health.MemoryLow);
        Assert.Equal(low, health.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_configured_lane_with_no_live_session_is_missing()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-api", "s-api"));

        var health = await Check();

        Assert.Equal(["ui"], health.MissingLanes);
        Assert.True(health.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_the_lane_check_is_skipped()
    {
        var health = await Check();

        Assert.Null(health.MissingLanes);
        Assert.False(health.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task More_copies_of_a_server_than_sessions_can_own_are_listed_as_duplicates_without_needing_attention()
    {
        await _world.Ready();
        AllLanesLive();
        _world.Processes.Add(new ProcessInfo(10, 1, "claude", 1, 100));
        foreach (var pid in Enumerable.Range(20, 4))
        {
            _world.Processes.Add(new ProcessInfo(pid, 10, McpServer, 1, 100));
        }

        var health = await Check();

        Assert.Equal([new DuplicateServer(McpServer, 4, 2)], health.Duplicates);
        Assert.False(health.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Heavy_lock_holders_are_listed()
    {
        await _world.Ready();
        AllLanesLive();
        _world.Resources.TakeLock(Environment.ProcessId);

        var health = await Check();

        Assert.Equal([Environment.ProcessId], health.HeavyLocks.Select(l => l.Pid.GetValueOrDefault()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_last_result_is_kept_for_status()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-api", "s-api"));
        Orphan();

        _ = await Check();

        Assert.Equal(
            "2026-10-15T05:00:00.0000000+00:00 attention: orphans=1 missing-lanes=1 memory-available=50%",
            (await _world.Values.Get(KvKeys.HealthLast, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Health_carries_each_lanes_context_and_the_compact_line_marks_a_lane_at_the_threshold_without_making_health_attend()
    {
        await _world.Ready();
        AllLanesLive();
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-api"), Agent: null, A.At(1)));
        _world.LaneFiles.Put(new LaneRecord("ui", SessionId.From("s-ui"), Agent: null, A.At(1)));
        _world.ClaudeTranscripts.PutUsage("s-api", 300_000, A.At(5));
        _world.ClaudeTranscripts.PutUsage("s-ui", 120_000, A.At(5));

        var health = await Check();

        Assert.Equal(["context: api 300k (recycle), ui 120k"], Host.Commands.ContextLine.Of(health.Context), StringComparer.Ordinal);
        Assert.False(health.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lanes_context_attends_exactly_at_recycleAt_and_not_one_token_below()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-api"), Agent: null, A.At(1)));
        var service = new Features.LanesContext.LanesContextService(_world.Config, _world.Context);
        _world.ClaudeTranscripts.PutUsage("s-api", 299_999, A.At(5));
        var below = await service.Handle(new Features.LanesContext.LanesContextRequest(), CancellationToken.None);
        _world.ClaudeTranscripts.PutUsage("s-api", 300_000, A.At(6));
        var at = await service.Handle(new Features.LanesContext.LanesContextRequest(), CancellationToken.None);

        Assert.False(below.Value.Attention);
        Assert.True(at.Value.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_context_line_lists_only_lanes_with_a_known_size_and_is_absent_when_none_is()
    {
        var known = new LaneContext("engine", SessionId.From("s"), "/t", 235_000, LastActivity: null, 300_000, ContextVerdict.Ok);
        var unknown = new LaneContext("ideas", Session: null, Transcript: null, Tokens: null, LastActivity: null, 300_000, ContextVerdict.Unknown, "x");

        Assert.Equal(["context: engine 235k"], Host.Commands.ContextLine.Of([known, unknown]), StringComparer.Ordinal);
        Assert.Empty(Host.Commands.ContextLine.Of([unknown]));
        Assert.Empty(Host.Commands.ContextLine.Of(lanes: null));
    }
}
