using Aw.Meta.Core.Usage;
using Aw.Meta.Features.Usage;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Usage;

public sealed class UsageServiceTests
{
    private readonly UsageWorld _w = new();

    private UsageService Service() => new(_w.World.Config, _w.Reader, _w.Standings);

    [Fact, Trait("Lane", "unit")]
    public async Task A_quiet_team_is_all_ok_and_not_an_attention()
    {
        await _w.Ready();

        var result = await Service().Handle(new UsageRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Attention);
        Assert.Equal(["api", "ui", "meta"], result.Value.Subjects.Select(s => s.Subject), StringComparer.Ordinal);
        Assert.All(result.Value.Subjects, s => Assert.Equal(UsageState.Ok, s.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_limited_lane_has_its_kind_and_reset_and_is_an_attention()
    {
        await _w.Ready();
        _w.LimitLane("ui");
        _w.World.Panes.Remove("t1-meta");
        _w.World.ClaudeTranscripts.FailFor(MetaWorld.MetaSessionId);

        var result = await Service().Handle(new UsageRequest(), CancellationToken.None);

        Assert.True(result.Value.Attention);
        var ui = result.Value.Subjects[1];
        Assert.Equal((UsageState.Limited, UsageKind.Session, new DateTimeOffset(2026, 10, 15, 5, 30, 0, TimeSpan.Zero)), (ui.State, ui.Kind, ui.ResetsAt));
        Assert.Equal(UsageState.Unknown, result.Value.Subjects[2].State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_subject_alone_is_not_an_attention()
    {
        await _w.Ready();
        _w.World.Panes.Remove("t1-ui");

        var result = await Service().Handle(new UsageRequest(), CancellationToken.None);

        Assert.False(result.Value.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_the_call_fails()
    {
        var result = await Service().Handle(new UsageRequest(), CancellationToken.None);

        Assert.True(result.IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_row_carries_the_lane_s_agent_model_and_usage_signal()
    {
        await _w.Ready();
        _ = await _w.World.Config.Save(new Core.Config.TeamConfig("t1", "/work", ["meta", "api", "ui"], "meta") { Routing = Routing.RoutingWorld.Cfg() }, CancellationToken.None);
        _w.World.LaneFiles.Put(new Core.Lanes.LaneRecord("ui", Core.Ids.SessionId.From("codex-ui"), new Core.Lanes.LaneAgent("codex", "codex", Hooks: true), A.At(1)));
        _w.World.CodexRollouts.Put("codex-ui", Routing.RoutingWorld.Rollout(88, 48) + "\n", A.At(55));

        var result = await Service().Handle(new UsageRequest(), CancellationToken.None);

        var (api, ui) = (result.Value.Subjects[0], result.Value.Subjects[1]);
        Assert.Equal(("claude-code", "opus", null), (api.Agent, api.Model, api.RemainingPct));
        Assert.Contains("Claude Code shows only the limit stop", api.SignalNote, StringComparison.Ordinal);
        Assert.Equal(("codex", "astra", 12d), (ui.Agent, ui.Model, ui.RemainingPct));
        Assert.Equal(["5h", "weekly"], ui.Windows!.Select(w => w.Name), StringComparer.Ordinal);
    }
}
