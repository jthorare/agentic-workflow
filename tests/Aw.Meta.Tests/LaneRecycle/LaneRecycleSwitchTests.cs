using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Routing;

namespace Aw.Meta.Tests.LaneRecycle;

// A lane that routing.lanes assigns to another agent is not cleared: its Claude exits and aw-meta-launch starts it on the routed agent.
public sealed class LaneRecycleSwitchTests
{
    private const string Digest = "/home/x/digests/t1-api-2026-10-17.md";
    private readonly MetaWorld _world = new();

    private sealed class ClaudeExits(IDelay inner, Action onWait) : IDelay
    {
        public ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken)
        {
            onWait();
            return inner.Wait(duration, cancellationToken);
        }
    }

    private LaneRecycleService Service(Action? onWait = null)
    {
        var delay = onWait is null ? (IDelay)_world.Delay : new ClaudeExits(_world.Delay, onWait);
        return new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            new RecycleRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.LaneFiles, _world.Log, delay, _world.Clock),
            new SwitchRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.Runner, _world.Log, delay, _world.Clock));
    }

    private async Task Ready(RoutingConfig? routing = null, int launchExit = 0)
    {
        await _world.Ready();
        var team = (await _world.Config.Load(CancellationToken.None)).Value;
        _ = await _world.Config.Save(team with { Routing = routing ?? RoutingWorld.Cfg() with { Lanes = new Dictionary<string, LaneRoute>(StringComparer.Ordinal) { ["api"] = new("codex", "gpt-5.5") } } }, CancellationToken.None);
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-old"), Agent: null, A.At(1)));
        _world.LaneSessions.Set("t1-api", "s-old");
        _world.Panes.Put("t1-api", "claude");
        _world.ClaudeTranscripts.PutUsage("s-old", 487_000, A.At(30));
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-5));
        _ = _world.Runner.When("--lanes api --no-open", launchExit == 0 ? "tmux t1-api: restarted (claude had exited)\n" : "boom", launchExit);
    }

    private void ClaudeIsGone() => _world.LaneSessions.End("t1-api");

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_routed_to_another_agent_exits_its_Claude_and_is_started_by_the_launcher_on_the_routed_agent()
    {
        await Ready();

        var result = await Service(ClaudeIsGone).Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal([("t1-api", "/exit")], _world.Panes.Woken);
        var launch = Assert.Single(_world.Runner.Requests);
        Assert.Equal(["aw-meta-launch", "--team", "t1", "--lanes", "api", "--no-open"], launch.Argv);
        Assert.Contains("codex", result.Value.Warning, StringComparison.Ordinal);
        Assert.Contains($"Read and continue from {Digest}", result.Value.Warning, StringComparison.Ordinal);
        var note = Assert.Single(_world.Log.Lines);
        Assert.Contains("switched lane api from claude-code to codex", note, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dry_run_lists_the_exit_and_the_launch_and_sends_nothing()
    {
        await Ready();

        var result = await Service().Handle(new LaneRecycleRequest("api", Digest, DryRun: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.Steps, s => s.Contains("/exit", StringComparison.Ordinal));
        Assert.Contains(result.Value.Steps, s => s.Contains("aw-meta-launch --team t1 --lanes api --no-open", StringComparison.Ordinal));
        Assert.Empty(_world.Panes.Woken);
        Assert.Empty(_world.Runner.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Claude_that_never_exits_is_AW_META_022_and_nothing_is_launched()
    {
        await Ready();

        var result = await Service().Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.RecycleTimedOut.Code, result.Error.Code);
        Assert.Empty(_world.Runner.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_launcher_that_fails_is_AW_META_012_with_its_output()
    {
        await Ready(launchExit: 1);

        var result = await Service(ClaudeIsGone).Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Contains("aw-meta-launch", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_whose_routing_names_the_agent_it_runs_is_cleared_as_before()
    {
        await Ready(RoutingWorld.Cfg() with { Lanes = new Dictionary<string, LaneRoute>(StringComparer.Ordinal) { ["api"] = new("claude-code", "sonnet") } });

        var result = await Service(() => _world.LaneSessions.Set("t1-api", "s-new")).Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal([("t1-api", "/clear"), ("t1-api", $"Read and continue from {Digest}")], _world.Panes.Woken);
        Assert.Empty(_world.Runner.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pane_error_after_exit_names_the_launch_command()
    {
        await Ready();
        _world.Panes.WakeFailure = "no such session: t1-api";

        var result = await Service(ClaudeIsGone).Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("aw-meta-launch --team t1 --lanes api --no-open", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_switch_warning_reminds_to_install_and_trust_the_target_agents_hooks()
    {
        await Ready();

        var result = await Service(ClaudeIsGone).Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.Contains("scripts/install-meta.sh --provider codex", result.Value.Warning, StringComparison.Ordinal);
        Assert.Contains("/hooks", result.Value.Warning, StringComparison.Ordinal);
    }
}
