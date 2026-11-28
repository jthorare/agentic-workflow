using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Features.Status;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.StopCheck;

// #4311: stop-check's recycle override reused the fail-open waiter read, so a stale waiter record plus a liveness error let every
// meta stop through. The override also counted toward the loop guard, and a removed lane's give-up kept status at ATTENTION.
public sealed class StopCheckWaiterFailClosedTests
{
    private const int WaiterPid = 4242;
    private readonly MetaWorld _world = new();

    private async Task OneOpenObligation()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"));
        _ = await _world.LaneFiles.RecordWaiter("meta", (WaiterPid, A.At(40)), CancellationToken.None);
    }

    private async Task<StopCheckResponse> MetaStops(bool hookActive = false)
    {
        var result = await new StopCheckService(_world.Session, _world.Sweep, _world.Guard, _world.Clock, _world.Beats)
            .Handle(new StopCheckRequest(SessionId.From(MetaWorld.MetaSessionId), MetaWorld.Transcript, hookActive, MaxBlocks: 3), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_waiter_record_with_a_liveness_error_still_blocks_the_meta_stop()
    {
        await OneOpenObligation();
        _world.Liveness.FailWith(MetaErrors.HostUnreadable.With("/proc: access denied"));

        var stop = await MetaStops();

        Assert.True(stop.Blocked);
        Assert.Null(stop.Warning);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Stops_let_through_for_a_live_waiter_do_not_count_toward_the_loop_guard()
    {
        await OneOpenObligation();
        _world.Liveness.Run(WaiterPid, A.At(39));
        for (var i = 0; i < 3; i++)
        {
            Assert.False((await MetaStops(hookActive: true)).Blocked);
        }

        _world.Liveness.Stop(WaiterPid);
        var stop = await MetaStops(hookActive: true);

        Assert.True(stop.Blocked);   // the three let-throughs were not counted, so the guard has not used its blocks
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_give_up_for_a_lane_no_longer_in_the_team_does_not_show_in_status()
    {
        await _world.Ready();
        _ = await _world.Values.Set(KvKeys.RecycleGaveUp, "gone|2026-11-24T10:00:00.0000000+00:00\napi|2026-11-24T11:00:00.0000000+00:00", CancellationToken.None);

        var result = await new StatusService(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Processes, _world.Clock)
            .Handle(new StatusRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.NotNull(result.Value.RecycleStuck);
        Assert.Contains("api at 2026-11-24 11:00Z", result.Value.RecycleStuck, StringComparison.Ordinal);
        Assert.DoesNotContain("gone", result.Value.RecycleStuck, StringComparison.Ordinal);
    }
}
