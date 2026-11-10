using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneRecycle;

// #2533: a lane asks the meta to recycle it; the recycle itself settles that message, so the meta need not dismiss it by hand.
public sealed class RecycleSettlesRequestTests
{
    private const string Digest = "/home/x/digests/t1-api-2026-10-17.md";
    private const string Ask = "api lane is at ~206k context. Please recycle it: `aw-meta lane-recycle api --digest /home/x/digests/t1-api-2026-10-17.md`";
    private readonly MetaWorld _world = new();

    private sealed class ClearTakesEffect(IDelay inner, Action onWait) : IDelay
    {
        public ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken)
        {
            onWait();
            return inner.Wait(duration, cancellationToken);
        }
    }

    private LaneRecycleService Service(Action? onWait = null)
    {
        var delay = onWait is null ? (IDelay)_world.Delay : new ClearTakesEffect(_world.Delay, onWait);
        var runner = new RecycleRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.LaneFiles, _world.Log, delay, _world.Clock, new RecycleSettler(_world.Messages, _world.Sync, _world.Clock));
        return new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            runner,
            new SwitchRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.Runner, _world.Log, delay, _world.Clock),
            new RecycleWaiter(new InMemoryWaiterLauncher(), _world.LaneFiles, new InMemoryProcessLiveness(), new LaneBeats(_world.Values, delay), runner, _world.Log, _world.Clock));
    }

    private async Task Ready()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-old"), Agent: null, A.At(1)));
        _world.LaneSessions.Set("t1-api", "s-old");
        _world.Panes.Put("t1-api", "claude");
        _world.ClaudeTranscripts.PutUsage("s-old", 206_000, A.At(30));
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-5));
    }

    private async Task Receive(string id, string fromName, string body) =>
        _ = await _world.Messages.RecordInbound("/t", 1, [InboxMessage.Of(new PeerMessage(MessageId.From(id), "sock-" + fromName, fromName, body, A.At(50)))], CancellationToken.None);

    private async Task<InboxMessage> Stored(string id) =>
        (await _world.Messages.Messages(CancellationToken.None)).Value.Single(m => m.Id == MessageId.From(id));

    private void NewSessionAppears() => _world.LaneSessions.Set("t1-api", "s-new");

    private async Task<Kernel.Result<LaneRecycleResponse>> Run(Action? onWait, bool dryRun = false) =>
        await Service(onWait).Handle(new LaneRecycleRequest("api", Digest, dryRun), CancellationToken.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_successful_recycle_dismisses_the_lanes_open_recycle_request_and_names_it_in_the_note_and_the_response()
    {
        await Ready();
        await Receive("m-ask", "t1-api", Ask);

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        var resolution = (await Stored("m-ask")).Resolution;
        Assert.Equal(MessageResolutionKind.Dismissed, resolution?.Kind);
        Assert.Contains("settled by lane-recycle api", resolution?.Reason, StringComparison.Ordinal);
        Assert.Equal(["m-ask"], result.Value.SettledMessages);
        Assert.Contains("m-ask", Assert.Single(_world.Log.Lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_DIGEST_READY_message_the_skill_tells_lanes_to_send_is_settled()
    {
        await Ready();
        await Receive("m-digest", "t1-api", "DIGEST READY " + Digest);

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(["m-digest"], result.Value.SettledMessages);
        Assert.Equal(MessageResolutionKind.Dismissed, (await Stored("m-digest")).Resolution?.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_DIGEST_READY_the_meta_read_this_turn_before_its_Stop_hook_synced_the_inbox_is_settled()
    {
        // #2647: the meta reads DIGEST READY mid-turn and recycles at once; only the Stop hook's stop-check stores the message.
        await Ready();
        var transcript = "/transcripts/" + MetaWorld.MetaSessionId + ".jsonl";
        _world.Transcripts.Append(transcript, MetaWorld.PeerLine("m-early", "t1-ui", "gate is green"));
        _ = await _world.Sync.Run(transcript, CancellationToken.None);
        _world.Transcripts.Append(transcript, MetaWorld.PeerLine("m-digest", "t1-api", "DIGEST READY " + Digest));

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(["m-digest"], result.Value.SettledMessages);
        Assert.Equal(MessageResolutionKind.Dismissed, (await Stored("m-digest")).Resolution?.Kind);
        Assert.Null((await Stored("m-early")).Resolution);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Another_sessions_transcript_is_not_read_when_settling()
    {
        await Ready();
        const string old = "/transcripts/s-old-meta.jsonl";
        _world.Transcripts.Append(old, MetaWorld.PeerLine("m-early", "t1-ui", "gate is green"));
        _ = await _world.Sync.Run(old, CancellationToken.None);
        _world.Transcripts.Append(old, MetaWorld.PeerLine("m-stale", "t1-api", "DIGEST READY " + Digest));

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Null(result.Value.SettledMessages);
        Assert.DoesNotContain((await _world.Messages.Messages(CancellationToken.None)).Value, m => m.Id == MessageId.From("m-stale"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_message_naming_the_digest_path_without_the_DIGEST_READY_prefix_is_settled()
    {
        await Ready();
        await Receive("m-path", "t1-api", "context is full, my notes are in " + Digest);

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess);
        Assert.Equal(["m-path"], result.Value.SettledMessages);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_message_that_only_says_recycle_in_passing_stays_open()
    {
        await Ready();
        await Receive("m-no", "t1-api", "don't recycle me, gate running");

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.SettledMessages);
        Assert.Null((await Stored("m-no")).Resolution);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_store_that_cannot_resolve_leaves_the_recycle_a_success_with_a_warning_and_still_logs_the_note()
    {
        await Ready();
        await Receive("m-ask", "t1-api", Ask);
        _world.Messages.FailResolves = true;

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Contains("settling the lane's request failed", result.Value.Warning, StringComparison.Ordinal);
        Assert.Contains("inbox-dismiss", result.Value.Warning, StringComparison.Ordinal);
        Assert.Contains("recycled lane api", Assert.Single(_world.Log.Lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dry_run_leaves_the_request_open()
    {
        await Ready();
        await Receive("m-ask", "t1-api", Ask);

        var result = await Run(onWait: null, dryRun: true);

        Assert.True(result.IsSuccess);
        Assert.Null((await Stored("m-ask")).Resolution);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_timed_out_recycle_AW_META_022_leaves_the_request_open()
    {
        await Ready();
        await Receive("m-ask", "t1-api", Ask);

        var result = await Run(onWait: null);

        Assert.Equal(MetaErrors.RecycleTimedOut.Code, result.Error.Code);
        Assert.Null((await Stored("m-ask")).Resolution);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_open_message_from_the_same_lane_that_does_not_mention_recycling_stays_open()
    {
        await Ready();
        await Receive("m-ask", "t1-api", Ask);
        await Receive("m-other", "t1-api", "Which flag does the gate take for the unit lane?");

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess);
        Assert.Equal(["m-ask"], result.Value.SettledMessages);
        Assert.Null((await Stored("m-other")).Resolution);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Another_lanes_recycle_request_stays_open()
    {
        await Ready();
        await Receive("m-ask", "t1-api", Ask);
        await Receive("m-ui", "t1-ui", "ui lane is at ~210k context. Please recycle it.");

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess);
        Assert.Equal(MessageResolutionKind.Dismissed, (await Stored("m-ask")).Resolution?.Kind);
        Assert.Null((await Stored("m-ui")).Resolution);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_request_whose_deferral_has_run_out_is_open_again_and_is_settled()
    {
        await Ready();
        await Receive("m-ask", "t1-api", Ask);
        _ = await _world.Messages.Resolve(MessageId.From("m-ask"), new MessageResolution(MessageResolutionKind.Deferred, "later", _world.Clock.GetUtcNow().AddMinutes(-1)), CancellationToken.None);

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess);
        Assert.Equal(["m-ask"], result.Value.SettledMessages);
        Assert.Equal(MessageResolutionKind.Dismissed, (await Stored("m-ask")).Resolution?.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_request_whose_deferral_is_still_running_stays_deferred()
    {
        await Ready();
        await Receive("m-ask", "t1-api", Ask);
        _ = await _world.Messages.Resolve(MessageId.From("m-ask"), new MessageResolution(MessageResolutionKind.Deferred, "later", _world.Clock.GetUtcNow().AddHours(2)), CancellationToken.None);

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.SettledMessages);
        Assert.Equal(MessageResolutionKind.Deferred, (await Stored("m-ask")).Resolution?.Kind);
    }
}
