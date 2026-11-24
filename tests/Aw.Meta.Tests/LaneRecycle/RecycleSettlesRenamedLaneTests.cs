using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneRecycle;

// #4108: a lane recycled once runs under its digest title ("Read and continue from ..."), not "t1-api". Its next DIGEST READY
// carries that title as the sender name, so the recycle finds it by the socket of the session the lane runs, as merge-check
// finds the reviewer's OK (#4103).
public sealed class RecycleSettlesRenamedLaneTests
{
    private const string Digest = "/home/x/digests/t1-api-2026-10-17.md";
    private const string DigestTitle = "Read and continue from /home/x/digests/t1-api-2026-10-16.md";
    private const string ApiSocket = "uds:/run/user/1000/cc-socks/5151.sock";
    private const string UiSocket = "uds:/run/user/1000/cc-socks/6262.sock";
    private readonly MetaWorld _world = new();

    private sealed class ClearTakesEffect(IDelay inner, Action onWait) : IDelay
    {
        public ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken)
        {
            onWait();
            return inner.Wait(duration, cancellationToken);
        }
    }

    private LaneRecycleService Service()
    {
        var delay = new ClearTakesEffect(_world.Delay, () => _world.LaneSessions.Set("t1-api", "s-new"));
        var settler = new RecycleSettler(_world.Messages, _world.Sync, _world.Clock, addresses: new LaneAddress(_world.LaneFiles, _world.LaneSessions, _world.Sessions));
        var runner = new RecycleRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.LaneFiles, _world.Log, delay, _world.Clock, settler);
        return new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            runner,
            new SwitchRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.Runner, _world.Log, delay, _world.Clock),
            new RecycleWaiter(new InMemoryWaiterLauncher(), _world.LaneFiles, new InMemoryProcessLiveness(), new LaneBeats(_world.Values, delay), runner, _world.Log, _world.Clock));
    }

    // The api lane was recycled before: its Claude (pid 5151) runs under the old digest's title. /clear keeps the process, so the
    // new session id it reports sends from the same socket.
    private async Task Ready()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-old"), Agent: null, A.At(1)));
        _world.LaneSessions.Set("t1-api", "s-old");
        _world.Panes.Put("t1-api", "claude");
        _world.ClaudeTranscripts.PutUsage("s-old", 206_000, A.At(30));
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-5));
        _world.Sessions.Add(new LiveSession(SessionId.From("s-new"), DigestTitle, 5151, "idle", "/work") { Address = ApiSocket });
    }

    private async Task Receive(string id, string socket, string fromName, string body) =>
        _ = await _world.Messages.RecordInbound("/t", 1, [InboxMessage.Of(new PeerMessage(MessageId.From(id), socket, fromName, body, A.At(50)))], CancellationToken.None);

    private async Task<InboxMessage> Stored(string id) =>
        (await _world.Messages.Messages(CancellationToken.None)).Value.Single(m => m.Id == MessageId.From(id));

    [Fact, Trait("Lane", "unit")]
    public async Task A_DIGEST_READY_sent_under_the_digest_title_of_the_lanes_last_recycle_is_settled()
    {
        await Ready();
        await Receive("m-digest", ApiSocket, DigestTitle, "DIGEST READY " + Digest);

        var result = await Service().Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(["m-digest"], result.Value.SettledMessages);
        Assert.Equal(MessageResolutionKind.Dismissed, (await Stored("m-digest")).Resolution?.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_DIGEST_READY_under_a_digest_title_from_another_lanes_socket_stays_open()
    {
        await Ready();
        await Receive("m-ui", UiSocket, DigestTitle, "DIGEST READY " + Digest);

        var result = await Service().Handle(new LaneRecycleRequest("api", Digest, DryRun: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Null(result.Value.SettledMessages);
        Assert.Null((await Stored("m-ui")).Resolution);
    }
}
