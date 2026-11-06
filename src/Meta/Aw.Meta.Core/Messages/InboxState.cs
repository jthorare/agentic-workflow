namespace Aw.Meta.Core.Messages;

/// <summary>The one rule for where an inbound message stands: open (owed a reply), answered, dismissed or deferred. The inbox list and the watch's open count both use it.</summary>
public static class InboxState
{
    /// <summary>The state name of an open message.</summary>
    public const string Open = "open";

    /// <summary>Reads a message's state.</summary>
    /// <param name="message">The message.</param>
    /// <param name="owed">Whether a reply obligation is open for it.</param>
    /// <param name="now">The current time, for a deferral still running.</param>
    /// <returns>open, answered, dismissed or deferred.</returns>
    public static string Of(InboxMessage message, bool owed, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.Resolution?.Kind == MessageResolutionKind.Dismissed ? "dismissed" : IsDeferred(message, now) ? "deferred" : owed ? Open : "answered";
    }

    /// <summary>Whether the message's deferral is still running.</summary>
    /// <param name="message">The message.</param>
    /// <param name="now">The current time.</param>
    /// <returns>True when deferred until later than now.</returns>
    public static bool IsDeferred(InboxMessage message, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.Resolution is { Kind: MessageResolutionKind.Deferred, Until: { } until } && until > now;
    }
}
