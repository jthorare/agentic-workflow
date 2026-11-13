using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Owner;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Reads a transcript past its stored offset, keeps each peer message once (the store dedupes on the message id) and moves
/// the offset in the same step. A new owner question is also queued for the owner. Used by inbox-sync and stop-check.
/// </summary>
/// <param name="transcripts">Reads the transcript.</param>
/// <param name="messages">Stores the messages and the offset.</param>
/// <param name="owner">Queues owner questions.</param>
/// <param name="clock">Dates a record that carries no timestamp.</param>
/// <param name="meta">Finds the meta's live transcript; null when the caller never syncs the meta's own transcript.</param>
/// <param name="obligations">Writes obligation.opened for each new message that is owed a reply; null to write none.</param>
public sealed class InboxSync(ITranscriptReader transcripts, IMessageStore messages, IOwnerStore owner, TimeProvider clock, MetaTranscriptResolver? meta, ObligationEventWriter? obligations = null)
{

    /// <summary>Creates a sync that can't find the meta's own transcript.</summary>
    /// <param name="transcripts">Reads the transcript.</param>
    /// <param name="messages">Stores the messages and the offset.</param>
    /// <param name="owner">Queues owner questions.</param>
    /// <param name="clock">Dates a record that carries no timestamp.</param>
    public InboxSync(ITranscriptReader transcripts, IMessageStore messages, IOwnerStore owner, TimeProvider clock)
        : this(transcripts, messages, owner, clock, meta: null, obligations: null)
    {
    }

    /// <summary>Syncs the transcript of the meta's live session, found by its registered name, whether or not a hook has stored an offset for it yet.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the meta's transcript was found and read; false when no meta is registered, no resolver is wired or no transcript file exists.</returns>
    public async ValueTask<Result<bool>> RunMeta(CancellationToken cancellationToken)
    {
        if (meta is null)
        {
            return false;
        }

        var path = await meta.Find(cancellationToken).ConfigureAwait(false);
        if (path.IsError)
        {
            return path.Error;
        }

        if (path.Value is not { } transcript)
        {
            return false;
        }

        var read = await Run(transcript, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : true;
    }

    /// <summary>Syncs one transcript.</summary>
    /// <param name="transcript">The transcript path.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The ids of the messages that were new, in transcript order.</returns>
    public async ValueTask<Result<IReadOnlyList<MessageId>>> Run(string transcript, CancellationToken cancellationToken)
    {
        var offset = await messages.Offset(transcript, cancellationToken).ConfigureAwait(false);
        if (offset.IsError)
        {
            return offset.Error;
        }

        var chunk = await transcripts.ReadFrom(transcript, offset.Value, cancellationToken).ConfigureAwait(false);
        if (chunk.IsError)
        {
            return chunk.Error;
        }

        var now = clock.GetUtcNow();
        var found = chunk.Value.Lines
            .Select(line => TranscriptRecordParser.Parse(line, now))
            .OfType<PeerMessage>()
            .Select(InboxMessage.Of)
            .ToList();
        var added = await messages.RecordInbound(transcript, chunk.Value.NextOffset, found, cancellationToken).ConfigureAwait(false);
        if (added.IsError)
        {
            return added.Error;
        }

        await WriteOpened(found.Where(m => added.Value.Contains(m.Id)), cancellationToken).ConfigureAwait(false);
        var queued = await QueueQuestions(found.Where(m => added.Value.Contains(m.Id)), cancellationToken).ConfigureAwait(false);
        return queued.IsError ? queued.Error : added;
    }

    // A new message that asks something owes a reply (the evaluator skips notices the same way); the sender's name is the lane that waits.
    private async ValueTask WriteOpened(IEnumerable<InboxMessage> fresh, CancellationToken cancellationToken)
    {
        if (obligations is null)
        {
            return;
        }

        foreach (var message in fresh.Where(m => !NoticeRule.IsNotice(m.Body)))
        {
            var who = message.FromName.Length > 0 ? message.FromName : message.From;
            await obligations.Opened(message.At, message.Id.Value, who, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<Unit>> QueueQuestions(IEnumerable<InboxMessage> fresh, CancellationToken cancellationToken)
    {
        foreach (var message in fresh.Where(m => m.Class.Kind == MessageKind.OwnerQuestion))
        {
            var asked = await owner.Ask(message.FromName.Length > 0 ? message.FromName : message.From, OwnerQuestionText.From(message.Body), message.At, cancellationToken).ConfigureAwait(false);
            if (asked.IsError)
            {
                return asked.Error;
            }
        }

        return Results.Ok();
    }
}
