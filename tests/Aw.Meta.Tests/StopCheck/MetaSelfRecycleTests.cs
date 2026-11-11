using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.StopCheck;

// #2498: the meta lane grew to 577k against a 200k mark because the only prompt to recycle itself was one META RECYCLE watch
// line per crossing. The stop-check now owes a recycle while the meta lane is at or over the mark and no waiter is recorded.
public sealed class MetaSelfRecycleTests
{
    private const string Transcript = "/transcripts/s-meta.jsonl";
    private readonly MetaWorld _world = new();

    private async Task Team(long metaTokens)
    {
        await _world.Ready();
        _ = await _world.Config.Save(new TeamConfig("t1", "/work", ["meta", "api", "ui"], "meta") { Context = new ContextConfig(200_000) }, CancellationToken.None);
        _world.LaneFiles.Put(new LaneRecord("meta", SessionId.From(MetaWorld.MetaSessionId), Agent: null, A.At(1)));
        _world.ClaudeTranscripts.PutUsage(MetaWorld.MetaSessionId, metaTokens, A.At(30));
        _world.Transcripts.Write(Transcript, string.Empty);   // the meta's own transcript, with no peer message in it
    }

    private async Task<StopCheckResponse> Stop()
    {
        var stop = await new StopCheckService(_world.Session, _world.Sweep, _world.Guard, _world.Clock, _world.Beats)
            .Handle(new StopCheckRequest(SessionId.From(MetaWorld.MetaSessionId), Transcript, HookActive: false, MaxBlocks: 3), CancellationToken.None);
        Assert.True(stop.IsSuccess, stop.IsError ? stop.Error.Message : string.Empty);
        return stop.Value;
    }

    private static bool OwesRecycle(StopCheckResponse stop) =>
        stop.Open.Any(o => o.NextCommand.Contains("lane-recycle meta", StringComparison.Ordinal) && o.NextCommand.Contains("--detach", StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public async Task A_meta_lane_over_the_mark_with_no_waiter_owes_a_detached_recycle_and_the_stop_blocks()
    {
        await Team(577_000);

        var stop = await Stop();

        Assert.True(OwesRecycle(stop), string.Join("; ", stop.Open.Select(o => o.Summary)));
        Assert.True(stop.Blocked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_meta_lane_under_the_mark_owes_no_recycle()
    {
        await Team(150_000);

        var stop = await Stop();

        Assert.False(OwesRecycle(stop));
        Assert.False(stop.Blocked);
    }

    // The detached waiter waits for a stop that is not blocked; an obligation still open after --detach would stall it.
    [Fact, Trait("Lane", "unit")]
    public async Task A_meta_lane_over_the_mark_with_a_waiter_recorded_owes_nothing_so_the_waiter_sees_the_stop()
    {
        await Team(577_000);
        _ = await _world.LaneFiles.RecordWaiter("meta", (4242, A.At(40)), CancellationToken.None);
        _world.Liveness.Run(4242, A.At(39));

        var stop = await Stop();

        Assert.False(OwesRecycle(stop));
        Assert.False(stop.Blocked);
    }

    // A SIGKILL, an OOM kill or a reboot skips the waiter's finally and leaves its record: a dead pid still owes the recycle.
    [Fact, Trait("Lane", "unit")]
    public async Task A_meta_lane_over_the_mark_whose_recorded_waiter_is_dead_still_owes_a_recycle()
    {
        await Team(577_000);
        _ = await _world.LaneFiles.RecordWaiter("meta", (4242, A.At(40)), CancellationToken.None);

        var stop = await Stop();

        Assert.True(OwesRecycle(stop));
        Assert.True(stop.Blocked);
    }
}
