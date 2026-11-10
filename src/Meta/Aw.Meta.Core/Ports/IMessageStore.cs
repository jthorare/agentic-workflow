using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;

namespace Aw.Meta.Core.Ports;

/// <summary>The meta session's inbound and outbound messages, transcript offsets and registered role.</summary>
public interface IMessageStore
{
    /// <summary>The byte offset the next read of <paramref name="transcript"/> starts at.</summary>
    /// <param name="transcript">The transcript path.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The offset, 0 when the transcript was never read.</returns>
    public ValueTask<Result<long>> Offset(string transcript, CancellationToken cancellationToken);

    /// <summary>Stores new messages and the new offset together; a message whose id is already stored is skipped.</summary>
    /// <param name="transcript">The transcript path.</param>
    /// <param name="offset">The new offset.</param>
    /// <param name="messages">The messages read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The ids that were new, in order.</returns>
    public ValueTask<Result<IReadOnlyList<MessageId>>> RecordInbound(string transcript, long offset, IReadOnlyList<InboxMessage> messages, CancellationToken cancellationToken);

    /// <summary>Every inbound message, oldest first.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The messages with their resolutions.</returns>
    public ValueTask<Result<IReadOnlyList<InboxMessage>>> Messages(CancellationToken cancellationToken);

    /// <summary>Dismisses or defers a message's reply obligation.</summary>
    /// <param name="id">The message.</param>
    /// <param name="resolution">How it is set aside.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or AW-META-005 for an unknown message.</returns>
    public ValueTask<Result<Unit>> Resolve(MessageId id, MessageResolution resolution, CancellationToken cancellationToken);

    /// <summary>Records that `inbox --once` showed these messages.</summary>
    /// <param name="ids">The messages.</param>
    /// <param name="at">When they were shown.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; an unknown id is skipped.</returns>
    public ValueTask<Result<Unit>> MarkShown(IReadOnlyList<MessageId> ids, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Stores a message the meta sent.</summary>
    /// <param name="sent">The send.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> RecordOutbound(Outbound sent, CancellationToken cancellationToken);

    /// <summary>Every message the meta sent, oldest first.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The sends.</returns>
    public ValueTask<Result<IReadOnlyList<Outbound>>> Sent(CancellationToken cancellationToken);

    /// <summary>The registered meta session.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The role, or null when none is registered.</returns>
    public ValueTask<Result<MetaRole?>> Role(CancellationToken cancellationToken);

    /// <summary>Registers the meta session, replacing any earlier one.</summary>
    /// <param name="role">The role.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> SetRole(MetaRole role, CancellationToken cancellationToken);
}
