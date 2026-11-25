using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneRecycle;

// #2683: on the meta's first turn after /clear the registered role still names the old session and the new transcript has no
// offsets row (only stop-check writes one), so lane-recycle's settle never read the transcript that holds the DIGEST READY.
public sealed class RecycleFirstTurnAfterClearTests
{
    private const string Digest = "/home/x/digests/t1-api-2026-10-17.md";
    private const string NewMetaSession = "s-meta-after-clear";
    private readonly MetaWorld _world = new();

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_turn_after_clear_settles_a_recycle_answered_in_the_new_transcript()
    {
        await _world.Ready(); // the role names the old session, s-meta
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-old"), Agent: null, A.At(1)));
        _world.LaneSessions.Set("t1-api", "s-old");
        _world.Panes.Put("t1-api", "claude");
        _world.ClaudeTranscripts.PutUsage("s-old", 206_000, A.At(30));
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-5));

        // /clear: the live session carrying the registered name has a new id; its transcript is new, with no offsets row.
        _world.Sessions.Add(A.Live("t1-meta", NewMetaSession));
        var newTranscript = "/transcripts/" + NewMetaSession + ".jsonl";
        _world.Transcripts.Append(newTranscript, MetaWorld.PeerLine("m-digest", "t1-api", "DIGEST READY " + Digest));

        var result = await Service().Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(["m-digest"], result.Value.SettledMessages);
        var stop = await new StopCheckService(_world.Session, _world.Sweep, _world.Guard, _world.Clock, _world.Beats)
            .Handle(new StopCheckRequest(SessionId.From(NewMetaSession), newTranscript, HookActive: false, MaxBlocks: 3), CancellationToken.None);
        Assert.True(stop.IsSuccess, stop.IsError ? stop.Error.Message : string.Empty);
        Assert.Empty(stop.Value.Open);
        Assert.False(stop.Value.Blocked);
    }

    private LaneRecycleService Service()
    {
        var delay = new NewSessionAppears(_world.Delay, () => _world.LaneSessions.Set("t1-api", "s-new"));
        var runner = new RecycleRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.LaneFiles, _world.Log, delay, _world.Clock, new RecycleSettler(_world.Messages, _world.Sync, _world.Clock));
        return new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            runner,
            new SwitchRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.Runner, _world.Log, delay, _world.Clock),
            new RecycleWaiter(new InMemoryWaiterLauncher(), _world.LaneFiles, new InMemoryProcessLiveness(), new LaneBeats(_world.Values, delay), runner, _world.Log, _world.Clock));
    }

    private sealed class NewSessionAppears(IDelay inner, Action onWait) : IDelay
    {
        public ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken)
        {
            onWait();
            return inner.Wait(duration, cancellationToken);
        }
    }
}
