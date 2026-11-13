using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Features.InboxDismiss;
using Aw.Meta.Features.OutboxRecord;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Events;

// An owed reply is opened when its message is stored and closed when it is sent or dismissed.
public sealed class ObligationEventsTests
{
    private readonly MetaWorld _world = new();
    private readonly InMemoryMetaEventStore _events = new();

    private async Task Sync(params (string Id, string Name, string Body)[] messages)
    {
        _world.Transcripts.Append(MetaWorld.Transcript, string.Concat(messages.Select(m => MetaWorld.PeerLine(m.Id, m.Name, m.Body))));
        var sync = new InboxSync(_world.Transcripts, _world.Messages, _world.Owner, _world.Clock, meta: null, new MetaEventSink(_events));
        _ = await sync.Run(MetaWorld.Transcript, CancellationToken.None);
    }

    private async Task Send(string to, string message)
    {
        var service = new OutboxRecordService(_world.Session, _world.Messages, _world.Reader, _world.Clock, new MetaEventSink(_events));
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

        Assert.Equal([("obligation.opened", "t1-api", """{"id":"m1","from":"t1-api","lane":"t1-api"}""")], await Written());
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

        Assert.Equal([("obligation.closed", "t1-api", """{"id":"m1","from":"t1-api","lane":"t1-api"}""")], await Written());
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

        var service = new DismissInboxService(_world.Messages, new MetaEventSink(_events), _world.Clock);
        var result = await service.Handle(new DismissInboxRequest(MessageId.From("m1"), "stale"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([("obligation.closed", "t1-api", """{"id":"m1","from":"t1-api","lane":"t1-api"}""")], await Written());
    }
}
