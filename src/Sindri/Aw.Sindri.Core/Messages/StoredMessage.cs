namespace Aw.Sindri.Core.Messages;

/// <summary>
/// A message as the sync writes it into messages.db (messages spec §8.1, the messages table). The text stays in messages.db: never in the ledger, a log or a
/// session's answer. <see cref="Mine"/> and <see cref="NamesMe"/> come from the writer, which knows my user id from auth.test.
/// </summary>
/// <param name="Conversation">The conversation; its row must be written first.</param>
/// <param name="Ts">The message's ts.</param>
/// <param name="ThreadTs">The thread root's ts for a reply or a root with replies; null for a plain top-level message.</param>
/// <param name="User">The author's user id.</param>
/// <param name="Text">The text, as Slack sent it.</param>
/// <param name="Mine">True when I wrote it.</param>
/// <param name="NamesMe">True when it names me ('&lt;@me&gt;'), not a broadcast.</param>
public sealed record StoredMessage(string Conversation, MessageTs Ts, MessageTs? ThreadTs, string User, string Text, bool Mine, bool NamesMe)
{
    /// <summary>Gets Slack's subtype ('bot_message'), or null.</summary>
    public string? Subtype { get; init; }

    /// <summary>Gets the ts of the last edit, or null.</summary>
    public MessageTs? EditedTs { get; init; }

    /// <summary>Gets the reply count Slack gives a thread root.</summary>
    public int ReplyCount { get; init; }

    /// <summary>Gets the newest reply's ts Slack gives a thread root, or null.</summary>
    public MessageTs? LatestReplyTs { get; init; }

    /// <summary>Gets a value indicating whether the row is known only from a search hit.</summary>
    public bool Partial { get; init; }

    /// <summary>Gets the blocks as JSON text, or null.</summary>
    public string? Blocks { get; init; }

    /// <summary>Gets the reactions as a JSON array.</summary>
    public string Reactions { get; init; } = "[]";

    /// <summary>Gets the file ids as a JSON array.</summary>
    public string FileIds { get; init; } = "[]";
}
