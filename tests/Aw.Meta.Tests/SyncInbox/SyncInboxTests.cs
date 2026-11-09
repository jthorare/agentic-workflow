using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.SyncInbox;
using Aw.Meta.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.SyncInbox;

public sealed class SyncInboxTests
{
    private const string Transcript = "/transcripts/meta.jsonl";

    private readonly InMemoryTranscriptReader _reader = new();
    private readonly InMemoryMessageStore _store = new();
    private readonly InMemoryOwnerStore _owner = new();
    private readonly SyncInboxService _service;

    public SyncInboxTests()
    {
        _service = new SyncInboxService(new InboxSync(_reader, _store, _owner, new FakeTimeProvider(A.T0)));
    }

    private static string UserPeer(string id, string name, string body) =>
        $$"""{"type":"user","origin":{"kind":"peer","from":"uds:/run/{{name}}.sock","msg_id":"{{id}}","name":"{{name}}","body":"{{body}}"},"timestamp":"2026-10-15T04:05:00.000Z"}""" + "\n";

    private static string Queued(string id, string name, string body) =>
        $$$"""{"type":"attachment","attachment":{"type":"queued_command","delivery_id":"d-{{{id}}}","origin":{"kind":"peer","from":"uds:/run/{{{name}}}.sock","msg_id":"{{{id}}}","name":"{{{name}}}","body":"{{{body}}}"}},"timestamp":"2026-10-15T04:06:00.000Z"}""" + "\n";

    private const string Noise = """{"type":"assistant","message":{"role":"assistant","content":"working"}}""" + "\n";

    private async Task<IReadOnlyList<string>> Sync()
    {
        var result = await _service.Handle(A.SyncInboxRequest().WithTranscript(Transcript).Build(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return [.. result.Value.NewMessages.Select(id => id.Value)];
    }

    private async Task<IReadOnlyList<Obligation>> Open() =>
        ObligationEvaluator.Evaluate(A.Facts() with { Messages = (await _store.Messages(CancellationToken.None)).Value });

    [Fact, Trait("Lane", "unit")]
    public async Task SyncInbox_records_each_peer_message_once()
    {
        _reader.Write(Transcript, Noise + UserPeer("m1", "t1-api", "ready #190 079c2541") + Noise + Queued("m2", "t1-ui", "hello"));

        var first = await Sync();
        var again = await Sync();
        _reader.Append(Transcript, UserPeer("m1", "t1-api", "ready #190 079c2541") + Queued("m3", "t1-api", "more"));
        var later = await Sync();

        Assert.Equal(["m1", "m2"], first, StringComparer.Ordinal);
        Assert.Empty(again);
        Assert.Equal(["m3"], later, StringComparer.Ordinal);
        Assert.Equal(["m1", "m2", "m3"], (await _store.Messages(CancellationToken.None)).Value.Select(m => m.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recorded_peer_message_opens_a_reply_obligation_until_it_is_answered()
    {
        _reader.Write(Transcript, UserPeer("m1", "t1-api", "please rebase"));
        _ = await Sync();

        var open = await Open();
        _ = await _store.RecordOutbound(A.Sent("t1-api", "done", 99), CancellationToken.None);
        var answered = ObligationEvaluator.Evaluate(A.Facts() with { Messages = (await _store.Messages(CancellationToken.None)).Value, Outbound = (await _store.Sent(CancellationToken.None)).Value });

        Assert.Equal([(ObligationKind.Reply, "m1"), (ObligationKind.RearmWatch, "watch")], open.Select(o => (o.Kind, o.Key)));
        Assert.Empty(answered);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_message_is_stored_with_its_sender_time_and_classification()
    {
        _reader.Write(Transcript, UserPeer("m1", "t1-api", "ready #190 079c2541"));
        _ = await Sync();

        var stored = Assert.Single((await _store.Messages(CancellationToken.None)).Value);

        Assert.Equal(("uds:/run/t1-api.sock", "t1-api", "ready #190 079c2541"), (stored.From, stored.FromName, stored.Body));
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 4, 5, 0, TimeSpan.Zero), stored.At);
        Assert.Equal(190, stored.Class.Pr);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_partial_trailing_line_waits_for_the_rest()
    {
        var whole = UserPeer("m2", "t1-ui", "second");
        _reader.Write(Transcript, UserPeer("m1", "t1-api", "first") + whole[..40]);

        var first = await Sync();
        _reader.Append(Transcript, whole[40..]);
        var second = await Sync();

        Assert.Equal(["m1"], first, StringComparer.Ordinal);
        Assert.Equal(["m2"], second, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_transcript_is_an_error_and_records_nothing()
    {
        var result = await _service.Handle(A.SyncInboxRequest().WithTranscript("/no/such.jsonl").Build(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.TranscriptUnreadable.Code, result.Error.Code);
        Assert.Empty((await _store.Messages(CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_blank_transcript_path_is_refused()
    {
        var result = new SyncInboxValidator().Validate(A.SyncInboxRequest().WithTranscript(" ").Build());

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_session_id_type_rejects_blank() => Assert.False(SessionId.TryFrom(" ", out _));

    [Fact, Trait("Lane", "unit")]
    public async Task An_owner_question_is_queued_once_with_its_lead_in_stripped()
    {
        _reader.Write(Transcript, UserPeer("m1", "t1-api", "OWNER QUESTION: postgres or sqlite?") + UserPeer("m2", "t1-ui", "hello"));

        _ = await Sync();
        _ = await Sync();

        var queued = Assert.Single((await _owner.List(includeAnswered: true, CancellationToken.None)).Value);
        Assert.Equal(("t1-api", "postgres or sqlite?"), (queued.AskedBy, queued.Text));
        Assert.Contains("m1", (await Open()).Select(o => o.Key), StringComparer.Ordinal);
    }
}
