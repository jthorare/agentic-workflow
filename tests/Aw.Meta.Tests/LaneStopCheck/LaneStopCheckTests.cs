using Aw.Meta.Features.LaneStopCheck;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneStopCheck;

public sealed class LaneStopCheckTests
{
    private readonly MetaWorld _world = new();

    private LaneStopCheckService Service() => new(_world.Config, _world.LaneMail, _world.Beats, _world.LaneGuard, _world.Clock);

    // The lane's own stop hook exports AW_META_HOOK; a call typed by hand does not.
    private async Task<LaneStopCheckResponse> Check(Core.Lanes.LaneIdentity lane, bool active = false, int max = 3, string? hook = "Stop")
    {
        var result = await Service().Handle(new LaneStopCheckRequest(lane, active, max) { HookEvent = hook }, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_that_owes_nothing_may_stop_and_the_heartbeat_says_it_is_idle()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");

        var response = await Check(lane);

        Assert.False(response.Blocked);
        Assert.Empty(response.Owed);
        Assert.True((await _world.Beats.Last("api", CancellationToken.None)).Value!.Stopped);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_delivered_message_with_no_reply_blocks_the_stop_and_a_reply_releases_it()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "rebase please", A.At(5), CancellationToken.None);
        _ = await _world.LaneMail.Deliver([1], A.At(6), CancellationToken.None);

        var blocked = await Check(lane);
        _ = await _world.LaneMail.Reply(lane, "done", A.At(7), CancellationToken.None);
        var released = await Check(lane);

        Assert.True(blocked.Blocked);
        Assert.Equal("rebase please", Assert.Single(blocked.Owed).Text);
        Assert.False(released.Blocked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_blocked_stop_leaves_the_lane_marked_busy_so_a_send_does_not_wake_it_mid_turn()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "rebase please", A.At(5), CancellationToken.None);
        _ = await _world.LaneMail.Deliver([1], A.At(6), CancellationToken.None);

        var blocked = await Check(lane);

        Assert.True(blocked.Blocked);
        Assert.False((await _world.Beats.Last("api", CancellationToken.None)).Value!.Stopped);
        Assert.False((await _world.Beats.IsIdle("api", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_undelivered_message_is_shown_by_the_block_and_marked_delivered()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "mid-turn note", A.At(5), CancellationToken.None);

        var blocked = await Check(lane);

        Assert.True(blocked.Blocked);
        Assert.True(Assert.Single((await _world.LaneMail.Owed("api", CancellationToken.None)).Value).Delivered);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_cap_lets_a_continuation_through_after_max_blocks_with_a_warning_an_incident_and_the_cap_mark()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "stuck", A.At(5), CancellationToken.None);

        var start = await Check(lane, active: false, max: 2);
        var first = await Check(lane, active: true, max: 2);
        var second = await Check(lane, active: true, max: 2);
        var third = await Check(lane, active: true, max: 2);

        Assert.Equal([true, true, true, false], [start.Blocked, first.Blocked, second.Blocked, third.Blocked]);
        Assert.StartsWith("aw-meta: stop allowed after 2 identical blocks", third.Warning, StringComparison.Ordinal);
        Assert.Contains(_world.Log.Lines, l => l.Contains("INCIDENT", StringComparison.Ordinal) && l.Contains("t1-api", StringComparison.Ordinal));
        var health = (await _world.LaneHealth.Read((await _world.Config.Load(CancellationToken.None)).Value, CancellationToken.None)).Value;
        Assert.Equal("obligation not delivered (stop cap)", Assert.Single(health, h => h.Lane == "api").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_turn_without_hook_active_resets_the_count_so_every_turn_gets_the_full_allowance()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "stuck", A.At(5), CancellationToken.None);
        _ = await Check(lane, active: false, max: 1);
        _ = await Check(lane, active: true, max: 1);
        var released = await Check(lane, active: true, max: 1);

        var again = await Check(lane, active: false, max: 1);

        Assert.False(released.Blocked);
        Assert.True(again.Blocked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_team_config_it_lets_the_stop_through_silently()
    {
        var response = await Check(new Core.Lanes.LaneIdentity("t1", "api"));

        Assert.False(response.Blocked);
        Assert.Null((await _world.Beats.Last("api", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_hand_typed_stop_check_is_not_hook_proof_and_does_not_make_the_lane_idle()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");

        var response = await Check(lane, hook: null);

        Assert.False(response.Blocked);
        Assert.Null((await _world.Beats.Last("api", CancellationToken.None)).Value);
        Assert.False((await _world.Beats.IsIdle("api", CancellationToken.None)).Value);
        Assert.Equal(_world.Clock.GetUtcNow(), (await _world.Beats.LastCli("api", CancellationToken.None)).Value);
        var health = Assert.Single((await _world.LaneHealth.Read((await _world.Config.Load(CancellationToken.None)).Value, CancellationToken.None)).Value);
        Assert.StartsWith("hooks not verified: only manual aw-meta runs seen", health.State, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_check_from_the_stop_hook_still_records_the_beat()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");

        _ = await Check(lane, hook: "Stop");

        Assert.True((await _world.Beats.Last("api", CancellationToken.None)).Value!.Stopped);
        Assert.True((await _world.Beats.IsIdle("api", CancellationToken.None)).Value);
    }
}
