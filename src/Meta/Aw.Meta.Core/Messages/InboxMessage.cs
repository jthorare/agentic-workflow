using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Messages;

/// <summary>A stored inbound message with its classification and any resolution.</summary>
/// <param name="Id">The message id.</param>
/// <param name="From">The sender's socket address.</param>
/// <param name="FromName">The sender's session name; empty when it had none.</param>
/// <param name="Body">The message text.</param>
/// <param name="At">When it was recorded in the transcript.</param>
/// <param name="Class">What the message is.</param>
public sealed record InboxMessage(MessageId Id, string From, string FromName, string Body, DateTimeOffset At, Classification Class)
{
    /// <summary>Set when the reply obligation was dismissed or deferred.</summary>
    public MessageResolution? Resolution { get; init; }

    /// <summary>When `inbox --once` last showed the message; null when it never did.</summary>
    public DateTimeOffset? ShownAt { get; init; }

    /// <summary>Stores <paramref name="peer"/> with its classification.</summary>
    /// <param name="peer">The parsed message.</param>
    /// <returns>The inbox message, unresolved.</returns>
    public static InboxMessage Of(PeerMessage peer) =>
        new((peer ?? throw new ArgumentNullException(nameof(peer))).Id, peer.From, peer.FromName, peer.Body, peer.At, MessageClassifier.Classify(peer.Body));
}
