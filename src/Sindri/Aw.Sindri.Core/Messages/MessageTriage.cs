namespace Aw.Sindri.Core.Messages;

/// <summary>The triage rules (messages spec §7.1, §7.2): the one definition of Needs reply, the class rules and the Done-and-return rule. Pure; M1b builds them.</summary>
public static class MessageTriage
{
    /// <summary>The one definition of Needs reply (§7.1): mine, the newest message not mine, no reply of mine after it, and not Done, snoozed or linked to an owned issue.</summary>
    /// <param name="row">The row.</param>
    /// <returns>True when the thread is in Needs reply.</returns>
    public static bool NeedsReply(ThreadRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        throw MessageStubs.NotBuilt("MessageTriage.NeedsReply", "M1b");
    }

    /// <summary>The class of a row (§7.2), from fixed rules checked in order: 3, then 2, then 1.</summary>
    /// <param name="row">The row.</param>
    /// <param name="requestPhrases">The profile key messages.requestPhrases.</param>
    /// <returns>The class.</returns>
    public static MessageClass Classify(ThreadRow row, IReadOnlyList<string> requestPhrases)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(requestPhrases);
        throw MessageStubs.NotBuilt("MessageTriage.Classify", "M1b");
    }

    /// <summary>Whether a Release 1 Needs reply list holds the row (call 21): class 3 and 2 only; class 1 shows in Unread.</summary>
    /// <param name="row">The row.</param>
    /// <returns>True when the Release 1 list holds it.</returns>
    public static bool InRelease1NeedsReply(ThreadRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        throw MessageStubs.NotBuilt("MessageTriage.InRelease1NeedsReply", "M1b");
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
}
