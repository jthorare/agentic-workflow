namespace Aw.Sindri.Core.Messages;

/// <summary>The triage rules (messages spec §7.1, §7.2): the one definition of Needs reply, the class rules and the Done-and-return rule. Pure.</summary>
public static class MessageTriage
{
    private const int SmallGroup = 3;

    /// <summary>
    /// The one definition of Needs reply (§7.1): mine, the newest message not mine, no reply of mine after it, and not Done, snoozed or linked to an
    /// owned issue. A read mark plays no part. The store clears <see cref="ThreadRow.LinkedToOwnedIssue"/> when a newer class 3 message returns the thread (§9.5).
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>True when the thread is in Needs reply.</returns>
    public static bool NeedsReply(ThreadRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Mine is not null && !row.NewestIsMine && !row.RepliedAfterNewest && !row.Done && !row.Snoozed && !row.LinkedToOwnedIssue;
    }

    /// <summary>
    /// The class of a row (§7.2), from fixed rules checked in order: 3, then 2, then 1. A thread that is not mine, or whose newest message is mine, has no
    /// class. The '?' and phrase rules read the words of the newest message only: a '?' in a link's URL, in code or in an emoji name does not count.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="requestPhrases">The profile key messages.requestPhrases; each matches whole words, any case.</param>
    /// <returns>The class.</returns>
    public static MessageClass Classify(ThreadRow row, IReadOnlyList<string> requestPhrases)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(requestPhrases);
        if (row.Mine is null || row.NewestIsMine)
        {
            return MessageClass.None;
        }

        if ((row.NamesMe || IsSmallDm(row)) && Asks(Mrkdwn.Parse(row.NewestText).Words(), requestPhrases))
        {
            return MessageClass.DirectQuestion;
        }

        return row.LinksWaitingOnMe ? MessageClass.ReviewAsk : MessageClass.Fyi;
    }

    /// <summary>Whether a Release 1 Needs reply list holds the row (call 21): class 3 and 2 only; class 1 shows in Unread.</summary>
    /// <param name="row">The row.</param>
    /// <returns>True when the Release 1 list holds it.</returns>
    public static bool InRelease1NeedsReply(ThreadRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return NeedsReply(row) && row.Class is MessageClass.DirectQuestion or MessageClass.ReviewAsk;
    }

    /// <summary>Whether a new message brings a Done row back (§7.2 "Done and return"): a mention, a DM or a reply in my thread does; a reaction does not.</summary>
    /// <param name="row">The Done row.</param>
    /// <param name="address">How the new message reaches me.</param>
    /// <returns>True when the row returns to Needs reply.</returns>
    public static bool ReturnsAfterDone(ThreadRow row, MessageAddress address)
    {
        ArgumentNullException.ThrowIfNull(row);
        _ = address;
        throw MessageStubs.NotBuilt("MessageTriage.ReturnsAfterDone", "M1h");
    }

    // a DM, or a group DM of 3 or fewer people; a group DM whose size is unknown (0) is not small
    private static bool IsSmallDm(ThreadRow row) =>
        row.Kind == ConversationKind.Dm || (row.Kind == ConversationKind.Mpim && row.Members is > 0 and <= SmallGroup);

    private static bool Asks(string words, IReadOnlyList<string> requestPhrases) =>
        words.Contains('?', StringComparison.Ordinal) || requestPhrases.Any(phrase => HoldsPhrase(words, phrase.Trim()));

    private static bool HoldsPhrase(string words, string phrase)
    {
        if (phrase.Length == 0)
        {
            return false;
        }

        for (var at = words.IndexOf(phrase, StringComparison.OrdinalIgnoreCase); at >= 0; at = words.IndexOf(phrase, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = at + phrase.Length;
            if ((at == 0 || !char.IsLetterOrDigit(words[at - 1])) && (end == words.Length || !char.IsLetterOrDigit(words[end])))
            {
                return true;
            }
        }

        return false;
    }
}
