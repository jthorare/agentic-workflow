using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>
/// The IMessageStore contract: the same tests run against the in-memory fake and every real adapter
/// (.agents/rules/csharp.md, Seams). A derived class supplies a fresh, empty store.
/// </summary>
public abstract class MessageStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty store.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IMessageStore> Create(CancellationToken cancellationToken);

    /// <summary>A transcript that was never read has offset zero.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_transcript_never_read_has_offset_zero()
    {
        var store = await Create(_none);

        var offset = await store.Offset("/t/a.jsonl", _none);

        Expect.That(offset.IsSuccess && offset.Value == 0, "offset 0");
    }

    /// <summary>Recording stores the messages and the offset, and a repeated id is not new.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Recording_dedupes_on_the_message_id_and_moves_the_offset()
    {
        var store = await Create(_none);
        var first = A.Inbox("m1", body: "ready #7 079c2541", minute: 1);
        var second = A.Inbox("m2", minute: 2);

        var added = await store.RecordInbound("/t/a.jsonl", 100, [first, second], _none);
        var again = await store.RecordInbound("/t/a.jsonl", 250, [second, A.Inbox("m3", minute: 3)], _none);
        var offset = await store.Offset("/t/a.jsonl", _none);
        var other = await store.Offset("/t/b.jsonl", _none);

        Expect.That(added.IsSuccess && added.Value.Select(i => i.Value).SequenceEqual(["m1", "m2"], StringComparer.Ordinal), "new ids are returned");
        Expect.That(again.IsSuccess && again.Value.Select(i => i.Value).SequenceEqual(["m3"], StringComparer.Ordinal), "a stored id is not new");
        Expect.That(offset.Value == 250 && other.Value == 0, "the offset is per transcript");
    }

    /// <summary>Every transcript with a stored offset is listed once, ordered by path.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Transcripts_lists_each_read_transcript_once_ordered_by_path()
    {
        var store = await Create(_none);
        var none = await store.Transcripts(_none);
        _ = await store.RecordInbound("/t/b.jsonl", 10, [A.Inbox("m1", minute: 1)], _none);
        _ = await store.RecordInbound("/t/a.jsonl", 20, [], _none);
        _ = await store.RecordInbound("/t/b.jsonl", 30, [], _none);
        var listed = await store.Transcripts(_none);

        Expect.That(none.IsSuccess && none.Value.Count == 0, "no transcript before the first read");
        Expect.That(listed.IsSuccess && listed.Value.SequenceEqual(["/t/a.jsonl", "/t/b.jsonl"], StringComparer.Ordinal), "each transcript once, ordered by path");
    }

    /// <summary>Messages read back with their classification, oldest first.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Messages_read_back_oldest_first_with_their_classification()
    {
        var store = await Create(_none);
        var late = A.Inbox("late", name: "api-1", body: "ready #190 079c2541", minute: 9);
        var early = A.Inbox("early", name: "ui-1", body: "ui lane here", minute: 1);
        _ = await store.RecordInbound("/t/a.jsonl", 1, [late, early], _none);

        var read = await store.Messages(_none);

        Expect.That(read.IsSuccess && read.Value.SequenceEqual([early, late]), "oldest first, every field kept");
        Expect.That(read.Value[1].Class.Pr == 190 && read.Value[0].Class.Lane == "ui", "classification kept");
    }

    /// <summary>A dismissed or deferred message keeps its resolution; an unknown one is refused.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Resolutions_are_stored_and_unknown_messages_are_refused()
    {
        var store = await Create(_none);
        _ = await store.RecordInbound("/t/a.jsonl", 1, [A.Inbox("m1"), A.Inbox("m2", minute: 1)], _none);
        var dismissed = new MessageResolution(MessageResolutionKind.Dismissed, "fyi");
        var deferred = new MessageResolution(MessageResolutionKind.Deferred, "gate running", A.At(30));

        var one = await store.Resolve(MessageId.From("m1"), dismissed, _none);
        var two = await store.Resolve(MessageId.From("m2"), deferred, _none);
        var missing = await store.Resolve(MessageId.From("nope"), dismissed, _none);
        var read = await store.Messages(_none);

        Expect.That(one.IsSuccess && two.IsSuccess, "known messages resolve");
        Expect.That(read.Value[0].Resolution == dismissed && read.Value[1].Resolution == deferred, "resolutions read back");
        Expect.That(missing.IsError && missing.Error.Code == MetaErrors.MessageNotFound.Code, "AW-META-005 for an unknown message");
    }

    /// <summary>Marking messages shown stores the time on those messages only; an unknown id is skipped.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Shown_messages_keep_the_time_they_were_shown()
    {
        var store = await Create(_none);
        _ = await store.RecordInbound("/t/a.jsonl", 1, [A.Inbox("m1"), A.Inbox("m2", minute: 1)], _none);

        var marked = await store.MarkShown([MessageId.From("m1"), MessageId.From("nope")], A.At(30), _none);
        var read = await store.Messages(_none);

        Expect.That(marked.IsSuccess, "marking succeeds, an unknown id is skipped");
        Expect.That(read.Value[0].ShownAt == A.At(30) && read.Value[1].ShownAt is null, "only the marked message has a shown time");
    }

    /// <summary>Sent messages read back in order, with their message id when known.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Sent_messages_read_back_in_order()
    {
        var store = await Create(_none);
        var first = A.Sent("t1-api", "ack", 1);
        var second = A.Sent("t1-ui", "LANE MAP v2", 2) with { Id = MessageId.From("out-1") };

        _ = await store.RecordOutbound(first, _none);
        _ = await store.RecordOutbound(second, _none);
        var read = await store.Sent(_none);

        Expect.That(read.IsSuccess && read.Value.SequenceEqual([first, second]), "sends read back with every field");
    }

    /// <summary>The meta role is absent until set and replaced when set again.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_role_is_set_and_replaced()
    {
        var store = await Create(_none);
        var none = await store.Role(_none);
        _ = await store.SetRole(new MetaRole(SessionId.From("s-1"), "t1-meta"), _none);
        _ = await store.SetRole(new MetaRole(SessionId.From("s-2"), string.Empty), _none);
        var role = await store.Role(_none);

        Expect.That(none.IsSuccess && none.Value is null, "no role at first");
        Expect.That(role.Value == new MetaRole(SessionId.From("s-2"), string.Empty), "the latest role wins");
    }
}
