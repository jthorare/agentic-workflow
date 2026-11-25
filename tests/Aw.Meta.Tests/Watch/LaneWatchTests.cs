using Aw.Meta.Core.Watch;

namespace Aw.Meta.Tests.Watch;

public sealed class LaneWatchTests
{
    private static readonly string[] _lanes = ["api", "ui"];
    private static readonly HashSet<string> _none = [];

    private static LaneWatch State(string text) => LaneWatch.Parse(text)!;

    [Fact, Trait("Lane", "unit")]
    public void A_fresh_install_records_the_live_lanes_and_reports_nothing()
    {
        var (lines, state, pending) = LaneWatch.Step(kept: null, _none, _lanes, ["ui"]);

        Assert.Empty(lines);
        Assert.Equal("up=api;down=", state.Format());
        Assert.Empty(pending);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_that_loses_its_session_is_reported_down_once_two_polls_agree()
    {
        var (first, afterFirst, pending) = LaneWatch.Step(State("up=api,ui;down="), _none, _lanes, ["ui"]);
        var (second, afterSecond, _) = LaneWatch.Step(afterFirst, pending, _lanes, ["ui"]);
        var (third, _, _) = LaneWatch.Step(afterSecond, _none, _lanes, ["ui"]);

        Assert.Empty(first);
        Assert.Equal(["LANE DOWN ui: no live session; restart it with aw-meta-launch --lanes ui"], second);
        Assert.Equal("up=api;down=ui", afterSecond.Format());
        Assert.Empty(third);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_missing_for_one_poll_only_is_not_reported()
    {
        var (_, state, pending) = LaneWatch.Step(State("up=api,ui;down="), _none, _lanes, ["ui"]);
        var (lines, after, waiting) = LaneWatch.Step(state, pending, _lanes, []);

        Assert.Empty(lines);
        Assert.Equal("up=api,ui;down=", after.Format());
        Assert.Empty(waiting);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_reported_down_that_has_a_session_again_is_reported_up()
    {
        var (lines, state, _) = LaneWatch.Step(State("up=api;down=ui"), _none, _lanes, []);

        Assert.Equal(["LANE UP ui"], lines);
        Assert.Equal("up=api,ui;down=", state.Format());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_never_seen_with_a_session_is_not_reported()
    {
        var (_, state, pending) = LaneWatch.Step(State("up=api;down="), _none, _lanes, ["ui"]);
        var (lines, _, _) = LaneWatch.Step(state, pending, _lanes, ["ui"]);

        Assert.Empty(lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_taken_out_of_the_config_is_forgotten()
    {
        var (_, state, _) = LaneWatch.Step(State("up=api,old;down=gone"), _none, _lanes, []);

        Assert.Equal("up=api,ui;down=", state.Format());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("api,ui")]
    [InlineData("down=ui;up=api")]
    public void Unreadable_kept_text_reads_as_none(string text) => Assert.Null(LaneWatch.Parse(text));
}
