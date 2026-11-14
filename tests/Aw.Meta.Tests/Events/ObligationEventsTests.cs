using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Features.InboxDismiss;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Features.LaneReply;
using Aw.Meta.Features.OutboxRecord;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Events;

// An owed reply is opened when its message is stored and closed when it is sent or dismissed.
public sealed class ObligationEventsTests
{
    private readonly MetaWorld _world = new();
    private readonly InMemoryMetaEventStore _events = new();

    private ObligationEventWriter Writer() => new(new MetaEventSink(_events), _world.Config, _world.Reader, _world.Clock);

    private async Task Dismiss(string id)
    {
        var result = await new DismissInboxService(_world.Messages, Writer()).Handle(new DismissInboxRequest(MessageId.From(id), "stale"), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
    }

    private async Task Sync(params (string Id, string Name, string Body)[] messages)
    {
        _world.Transcripts.Append(MetaWorld.Transcript, string.Concat(messages.Select(m => MetaWorld.PeerLine(m.Id, m.Name, m.Body))));
        var sync = new InboxSync(_world.Transcripts, _world.Messages, _world.Owner, _world.Clock, meta: null, Writer());
        _ = await sync.Run(MetaWorld.Transcript, CancellationToken.None);
    }

    private async Task Send(string to, string message)
    {
        var service = new OutboxRecordService(_world.Session, _world.Messages, _world.Reader, _world.Clock, Writer());
        var result = await service.Handle(new OutboxRecordRequest(SessionId.From(MetaWorld.MetaSessionId), to, message, Id: null), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
    }

    private async Task<IReadOnlyList<(string Kind, string? Lane, string Fields)>> Written() =>
        [.. (await _events.Read(0, kind: null, lane: null, 100, CancellationToken.None)).Value.Events.Select(e => (e.Kind, e.Lane, e.Fields))];

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_message_that_asks_something_appends_obligation_opened_once()
    {
        await _world.Ready();

        await Sync(("m1", "t1-api", "please rebase"));
        await Sync();

        Assert.Equal([("obligation.opened", "api", """{"id":"m1","from":"t1-api","lane":"api"}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_notice_opens_no_obligation()
    {
        await _world.Ready();

        await Sync(("m1", "t1-api", "FYI: lunch"));

        Assert.Empty(await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_send_to_the_peer_appends_obligation_closed_for_that_reply_only()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "please rebase");
        await _world.Receive("m2", "t1-ui", "and me");

        await Send("t1-api", "done");

        Assert.Equal([("obligation.closed", "api", """{"id":"m1","from":"t1-api","lane":"api"}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_send_that_closes_nothing_appends_nothing()
    {
        await _world.Ready();

        await Send("t1-api", "hello");

        Assert.Empty(await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dismissing_a_message_appends_obligation_closed()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "please rebase");

        await Dismiss("m1");

        Assert.Equal([("obligation.closed", "api", """{"id":"m1","from":"t1-api","lane":"api"}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dismissing_twice_appends_obligation_closed_once()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "please rebase");

        await Dismiss("m1");
        await Dismiss("m1");

        _ = Assert.Single(await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dismissing_an_answered_message_appends_no_second_closed()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "please rebase");
        await Send("t1-api", "done");

        await Dismiss("m1");

        _ = Assert.Single(await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dismissing_a_notice_appends_nothing()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "FYI: lunch");

        await Dismiss("m1");

        Assert.Empty(await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sender_without_the_team_prefix_keeps_its_whole_name_as_the_lane()
    {
        await _world.Ready();

        await Sync(("m1", "ops", "please rebase"));

        Assert.Equal([("obligation.opened", "ops", """{"id":"m1","from":"ops","lane":"ops"}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Settling_a_recycle_request_appends_one_obligation_closed_per_settled_id()
    {
        await _world.Ready();
        const string digest = "/d/t1-api.md";
        await _world.Receive("m1", "t1-api", "DIGEST READY " + digest);
        await _world.Receive("m2", "t1-api", "recycle me, digest " + digest);
        _world.Clock.Advance(TimeSpan.FromMinutes(1));

        var settler = new RecycleSettler(_world.Messages, _world.Sync, _world.Clock, Writer());
        var settled = await settler.Settle(new RecyclePlan("t1", "api", SessionId.From("s-old"), OldTokens: null, digest), DateTimeOffset.MaxValue, CancellationToken.None);

        Assert.True(settled.IsSuccess, settled.IsError ? settled.Error.Message : string.Empty);
        Assert.Equal(["m1", "m2"], settled.Value.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(
            [("obligation.closed", "api", """{"id":"m1","from":"t1-api","lane":"api"}"""), ("obligation.closed", "api", """{"id":"m2","from":"t1-api","lane":"api"}""")],
            [.. (await Written()).OrderBy(static e => e.Fields, StringComparer.Ordinal)]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_reply_appends_obligation_opened_and_the_meta_send_closes_it()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");

        var reply = await new LaneReplyService(_world.Config, _world.LaneFiles, _world.LaneMail, _world.Log, _world.Clock, Writer()).Handle(new LaneReplyRequest(lane, "all green"), CancellationToken.None);
        Assert.True(reply.IsSuccess, reply.IsError ? reply.Error.Message : string.Empty);
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        await Send("t1-api", "thanks");

        Assert.Equal(
            [("obligation.opened", "api", """{"id":"lane:api:1","from":"t1-api","lane":"api"}"""), ("obligation.closed", "api", """{"id":"lane:api:1","from":"t1-api","lane":"api"}""")],
            await Written());
    }
}
