using System.Globalization;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.StopCheck;

// #4264: the meta nearly always owes something, so a blocked stop never let its recycle waiter see a turn end. A live waiter
// now lets the stop through; the open obligations stay in the store and carry over to the next session.
public sealed class StopCheckRecycleCarryOverTests
{
    private const int WaiterPid = 4242;
    private readonly MetaWorld _world = new();

    private async Task OneOpenObligation()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"));
        _ = await _world.LaneFiles.RecordWaiter("meta", (WaiterPid, A.At(40)), CancellationToken.None);
    }

    private async Task<StopCheckResponse> MetaStops()
    {
        var result = await new StopCheckService(_world.Session, _world.Sweep, _world.Guard, _world.Clock, _world.Beats)
            .Handle(new StopCheckRequest(SessionId.From(MetaWorld.MetaSessionId), MetaWorld.Transcript, HookActive: false, MaxBlocks: 3), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_meta_stop_with_an_open_obligation_and_a_live_recycle_waiter_is_let_through_and_the_obligation_carries_over()
    {
        await OneOpenObligation();
        _world.Liveness.Run(WaiterPid, A.At(39));

        var stop = await MetaStops();

        Assert.False(stop.Blocked);
        var reply = Assert.Single(stop.Open, o => o.Kind == ObligationKind.Reply);
        Assert.NotNull(stop.Warning);
        Assert.Contains("letting the turn end for the pending recycle", stop.Warning, StringComparison.Ordinal);
        Assert.Contains(stop.Open.Count == 1 ? "1 open obligation carries over" : string.Create(CultureInfo.InvariantCulture, $"{stop.Open.Count} open obligations carry over"), stop.Warning, StringComparison.Ordinal);
        Assert.Contains(reply.Summary, stop.Warning, StringComparison.Ordinal);
        Assert.True((await _world.Beats.Last("meta", CancellationToken.None)).Value?.Stopped ?? false);
        _ = Assert.Single((await MetaStops()).Open, o => o.Kind == ObligationKind.Reply);   // nothing was cleared: the next stop-check still sees it
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_meta_stop_with_an_open_obligation_and_a_dead_recycle_waiter_pid_is_still_blocked()
    {
        await OneOpenObligation();

        var stop = await MetaStops();

        Assert.True(stop.Blocked);
        Assert.Null(stop.Warning);
        Assert.False((await _world.Beats.Last("meta", CancellationToken.None)).Value?.Stopped ?? false);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_meta_stop_with_an_open_obligation_and_no_recycle_waiter_is_still_blocked()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"));

        var stop = await MetaStops();

        Assert.True(stop.Blocked);
        Assert.Null(stop.Warning);
    }
}
