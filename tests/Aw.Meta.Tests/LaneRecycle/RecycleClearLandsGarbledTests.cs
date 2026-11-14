using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneRecycle;

// #3443: the lane's prompt ate "/cl" of "/clear", "ear" was submitted as a prompt and the recycle waited 180 s for a session id that never came.
public sealed class RecycleClearLandsGarbledTests
{
    private const string Digest = "/home/x/digests/t1-api-2026-10-17.md";
    private readonly MetaWorld _world = new();

    private LaneRecycleService Service()
    {
        var runner = new RecycleRunner(new PaneSubmitter(_world.Panes, _world.Delay), _world.LaneSessions, _world.LaneFiles, _world.Log, _world.Delay, _world.Clock, new RecycleSettler(_world.Messages, _world.Sync, _world.Clock));
        return new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            runner,
            new SwitchRunner(new PaneSubmitter(_world.Panes, _world.Delay), _world.LaneSessions, _world.Runner, _world.Log, _world.Delay, _world.Clock),
            new RecycleWaiter(new InMemoryWaiterLauncher(), _world.LaneFiles, new InMemoryProcessLiveness(), new LaneBeats(_world.Values, _world.Delay), runner, _world.Log, _world.Clock));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_clear_that_lands_without_its_first_keys_submits_nothing_and_fails_without_the_180_s_wait()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-old"), Agent: null, A.At(1)));
        _world.LaneSessions.Set("t1-api", "s-old");
        _world.Panes.Put("t1-api", "claude");
        _world.ClaudeTranscripts.PutUsage("s-old", 487_000, A.At(30));
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-5));
        _world.Panes.EatNextTyped = 3;

        var result = await Service().Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Contains("'ear'", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, _world.Panes.Enters);
        Assert.Empty(_world.Panes.Woken);
        Assert.True(_world.Delay.Waits.Sum(static w => w.TotalSeconds) < 30, string.Join(", ", _world.Delay.Waits));
        Assert.Empty(_world.Log.Lines);
        Assert.Equal(string.Empty, _world.Panes.InputBox("t1-api"));
    }
}
