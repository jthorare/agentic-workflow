using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.LaneRecycle;

// #4264: a waiter that gave up only wrote a log NOTE, so nobody saw the meta lane stop recycling. The give-up is now an
// ATTENTION line in `aw-meta status` until the next successful recycle of that lane.
public sealed class RecycleGiveUpStatusTests
{
    private const string Digest = "/home/x/digests/t1-meta-2026-11-09.md";
    private const string OldSession = "s-meta-old";
    private const int WaiterPid = 4242;
    private readonly MetaWorld _world = new();

    private async Task Ready()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("meta", SessionId.From(OldSession), Agent: null, A.At(1)));
        _world.LaneSessions.Set("t1-meta", OldSession);
        _world.Panes.Put("t1-meta", "claude");
        _world.ClaudeTranscripts.PutUsage(OldSession, 410_000, A.At(30));
    }

    private async Task<Kernel.Result<LaneRecycleResponse>> WaitIdle(HookingDelay delay)
    {
        // The digest is written just before each waiter starts, as the meta does before --detach.
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-5));
        _ = await _world.LaneFiles.RecordWaiter("meta", (WaiterPid, _world.Clock.GetUtcNow()), CancellationToken.None);
        var runner = new RecycleRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.LaneFiles, _world.Log, delay, _world.Clock, new RecycleSettler(_world.Messages, _world.Sync, _world.Clock));
        var service = new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            runner,
            new SwitchRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.Runner, _world.Log, delay, _world.Clock),
            new RecycleWaiter(new InMemoryWaiterLauncher(), _world.LaneFiles, new InMemoryProcessLiveness(), new LaneBeats(_world.Values, delay), runner, _world.Log, _world.Clock));
        return await service.Handle(new LaneRecycleRequest("meta", Digest, DryRun: false, Detach: false, WaitIdle: true), CancellationToken.None);
    }

    private async Task<StatusResponse> Status()
    {
        var result = await new StatusService(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Processes, _world.Clock)
            .Handle(new StatusRequest(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private Task<Kernel.Result<LaneRecycleResponse>> GiveUp() => WaitIdle(new HookingDelay(_world.Delay, _world.Clock, static (_, _) => ValueTask.CompletedTask));

    // The turn ends on the third poll; the new session shows up once /clear was typed.
    private Task<Kernel.Result<LaneRecycleResponse>> Recycle() => WaitIdle(new HookingDelay(_world.Delay, _world.Clock, async (call, now) =>
    {
        if (call == 3)
        {
            _ = await _world.Beats.Record("meta", stopped: true, now, CancellationToken.None);
        }

        if (_world.Panes.Woken.Count > 0)
        {
            _world.LaneSessions.Set("t1-meta", "s-meta-new");
        }
    }));

    [Fact, Trait("Lane", "unit")]
    public async Task A_waiter_that_gives_up_puts_an_ATTENTION_line_naming_the_lane_in_the_status()
    {
        await Ready();
        Assert.Null((await Status()).RecycleStuck);

        var result = await GiveUp();

        Assert.True(result.IsError);
        var line = (await Status()).RecycleStuck;
        Assert.NotNull(line);
        Assert.StartsWith("ATTENTION", line, StringComparison.Ordinal);
        Assert.Contains("meta", line, StringComparison.Ordinal);
        Assert.Contains("30 min", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_later_successful_recycle_of_that_lane_clears_the_ATTENTION_line()
    {
        await Ready();
        _ = await GiveUp();
        Assert.NotNull((await Status()).RecycleStuck);

        var result = await Recycle();

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Null((await Status()).RecycleStuck);
    }
}
