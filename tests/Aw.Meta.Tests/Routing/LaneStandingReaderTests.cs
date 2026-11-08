using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Usage;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Routing;

public sealed class LaneStandingReaderTests
{
    private readonly RoutingWorld _w = new();

    private async Task<IReadOnlyList<Core.Routing.LaneStanding>> Look()
    {
        var team = (await _w.U.World.Config.Load(CancellationToken.None)).Value;
        var readings = (await _w.U.Reader.Read(team, CancellationToken.None)).Value;
        return await _w.U.Standings.Read(team, readings, CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Claude_lane_has_the_default_model_and_says_Claude_shows_no_remaining_share()
    {
        await _w.Ready();

        var api = (await Look()).Single(s => s.Lane == "api");

        Assert.Equal(("claude-code", "claude-code", "opus", UsageState.Ok), (api.Agent, api.RunningAgent, api.Model, api.State));
        Assert.Null(api.Signal);
        Assert.Contains("Claude Code shows only the limit stop", api.Note, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Codex_lane_carries_the_windows_from_its_rollout()
    {
        await _w.Ready();
        _w.CodexLane("ui", RoutingWorld.Rollout(88, 48));

        var ui = (await Look()).Single(s => s.Lane == "ui");

        Assert.Equal("codex", ui.Agent);
        Assert.Equal(12d, ui.RemainingPct);
        Assert.Equal(["5h", "weekly"], ui.Signal!.Windows.Select(w => w.Name), StringComparer.Ordinal);
        Assert.False(ui.Limited);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Codex_lane_whose_provider_reports_its_limit_is_limited()
    {
        await _w.Ready();
        _w.CodexLane("ui", RoutingWorld.Rollout(100, 48));

        Assert.True((await Look()).Single(s => s.Lane == "ui").Limited);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Codex_lane_with_no_rollout_says_so_instead_of_guessing()
    {
        await _w.Ready();
        _w.CodexLane("ui", rollout: null);

        var ui = (await Look()).Single(s => s.Lane == "ui");

        Assert.Null(ui.Signal);
        Assert.Equal("no Codex rollout found for the lane's session", ui.Note);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Codex_lane_with_no_session_yet_says_so()
    {
        await _w.Ready();
        _w.U.World.LaneFiles.Put(new LaneRecord("ui", Session: null, new LaneAgent("codex", "codex", Hooks: true)));

        Assert.Equal("the lane has reported no session id yet", (await Look()).Single(s => s.Lane == "ui").Note);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Cursor_lane_is_unknown_because_Cursor_shows_nothing()
    {
        await _w.Ready();
        _w.U.World.LaneFiles.Put(new LaneRecord("ui", SessionId.From("cur-1"), new LaneAgent("cursor", "agent", Hooks: true), A.At(1)));

        var ui = (await Look()).Single(s => s.Lane == "ui");

        Assert.Equal("cursor", ui.Agent);
        Assert.Null(ui.Signal);
        Assert.Equal("cursor shows no usage figure", ui.Note);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_entry_in_routing_sets_the_assigned_agent_and_model_and_marks_a_relaunch_due()
    {
        await _w.Ready(RoutingWorld.Cfg() with { Lanes = new Dictionary<string, LaneRoute>(StringComparer.Ordinal) { ["ui"] = new("codex", "astra") } });

        var ui = (await Look()).Single(s => s.Lane == "ui");

        Assert.Equal(("codex", "claude-code", "astra", true), (ui.Agent, ui.RunningAgent, ui.Model, ui.PendingRelaunch));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_entry_with_only_a_model_takes_the_agent_that_lists_it()
    {
        await _w.Ready(RoutingWorld.Cfg() with { Lanes = new Dictionary<string, LaneRoute>(StringComparer.Ordinal) { ["api"] = new(Model: "luna") } });

        var api = (await Look()).Single(s => s.Lane == "api");

        Assert.Equal(("codex", "luna"), (api.Agent, api.Model));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_team_agent_is_what_a_lane_with_no_record_runs()
    {
        await _w.Ready(agent: "codex");

        Assert.All(await Look(), s => Assert.Equal("codex", s.RunningAgent));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_meta_is_in_the_standings_with_its_own_subject_name()
    {
        await _w.Ready();

        Assert.Equal(["api", "ui", "meta"], (await Look()).Select(s => s.Lane), StringComparer.Ordinal);
    }
}
