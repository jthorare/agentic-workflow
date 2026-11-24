namespace Aw.Sindri.Core.Messages;

/// <summary>
/// A Needs reply or Unread row (messages spec §7.3): one thread, or one DM, with the facts triage reads (§7.1, §7.2). Its key is
/// 'slack:thread:&lt;workspace&gt;/&lt;conversation&gt;/&lt;thread ts&gt;', or 'slack:dm:&lt;workspace&gt;/&lt;conversation&gt;' for a DM (Home §3.1).
/// </summary>
/// <param name="Key">The row key.</param>
/// <param name="Kind">The conversation's kind.</param>
/// <param name="Conversation">The Slack conversation.</param>
/// <param name="Newest">The newest message's ts.</param>
public sealed record ThreadRow(string Key, ConversationKind Kind, string Conversation, MessageTs Newest)
{
    /// <summary>Gets the thread root's ts; null for a DM, whose row is the conversation.</summary>
    public MessageTs? Thread { get; init; }

    /// <summary>Gets the people in the DM or group DM; 0 for a channel.</summary>
    public int Members { get; init; }

    /// <summary>Gets why the thread is mine, or null when it is not.</summary>
    public MineReason? Mine { get; init; }

    /// <summary>Gets a value indicating whether I wrote the newest message.</summary>
    public bool NewestIsMine { get; init; }

    /// <summary>Gets a value indicating whether I wrote a reply after the newest message not mine.</summary>
    public bool RepliedAfterNewest { get; init; }

    /// <summary>Gets a value indicating whether the newest message names me (not a broadcast).</summary>
    public bool NamesMe { get; init; }

    /// <summary>Gets the newest message's text, for the '?' and request-phrase rules; never in a session's answer (W17).</summary>
    public string NewestText { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the thread links a PR or issue that waits on me.</summary>
    public bool LinksWaitingOnMe { get; init; }

    /// <summary>Gets what the thread's 'about' links block (Home §4.1).</summary>
    public int Blocks { get; init; }

    /// <summary>Gets a value indicating whether the row is Done (Home's row state).</summary>
    public bool Done { get; init; }

    /// <summary>Gets a value indicating whether the row has a live snooze (Home's row state).</summary>
    public bool Snoozed { get; init; }

    /// <summary>Gets a value indicating whether the thread links an issue that has an owner.</summary>
    public bool LinkedToOwnedIssue { get; init; }

    /// <summary>Gets the row's class.</summary>
    public MessageClass Class { get; init; }

    /// <summary>The key of a DM's row.</summary>
    /// <param name="workspace">The Slack workspace.</param>
    /// <param name="conversation">The DM.</param>
    /// <returns>'slack:dm:&lt;workspace&gt;/&lt;conversation&gt;'.</returns>
    /// <exception cref="ArgumentException">A part is empty or holds a '/'.</exception>
    public static string DmKey(string workspace, string conversation) =>
        $"slack:dm:{Part(workspace, nameof(workspace))}/{Part(conversation, nameof(conversation))}";

    /// <summary>The key of a channel thread's row.</summary>
    /// <param name="workspace">The Slack workspace.</param>
    /// <param name="conversation">The channel.</param>
    /// <param name="thread">The thread root's ts.</param>
    /// <returns>'slack:thread:&lt;workspace&gt;/&lt;conversation&gt;/&lt;thread ts&gt;'.</returns>
    /// <exception cref="ArgumentException">A part is empty or holds a '/', or the thread is not a Slack ts.</exception>
    public static string ThreadKey(string workspace, string conversation, MessageTs thread)
    {
        if (!MessageTs.TryParse(thread.Value, out _))
        {
            throw new ArgumentException("The thread is not a Slack ts.", nameof(thread));
        }

        return $"slack:thread:{Part(workspace, nameof(workspace))}/{Part(conversation, nameof(conversation))}/{thread.Value}";
    }

    // a key part is a Slack id: never empty, never a '/', which would split the key
    private static string Part(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Contains('/', StringComparison.Ordinal) ? throw new ArgumentException("A key part holds no '/'.", name) : value;
    }
}
