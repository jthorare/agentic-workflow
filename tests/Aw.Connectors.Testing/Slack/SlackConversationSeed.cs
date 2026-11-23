namespace Aw.Connectors.Testing.Slack;

/// <summary>A conversation the fake Slack knows: what 'users.conversations' lists and 'conversations.info' describes.</summary>
/// <param name="Id">Slack's id ('C…' for a channel, 'D…' for a DM, 'G…' for a group DM).</param>
/// <param name="Kind">The kind.</param>
/// <param name="Name">The channel name; Slack sends none for a DM.</param>
public sealed record SlackConversationSeed(string Id, SlackConversationKind Kind, string Name)
{
    /// <summary>For a DM, the other person's user id (Slack's 'user').</summary>
    public string? DmUser { get; init; }

    /// <summary>Slack's 'last_read' ts, which 'conversations.info' returns; null sends none.</summary>
    public string? LastRead { get; init; }

    /// <summary>Slack's 'unread_count' and 'unread_count_display' on 'conversations.info'; null sends none.</summary>
    public int? UnreadCount { get; init; }

    /// <summary>Whether the token's user is in it; 'users.conversations' lists only those.</summary>
    public bool IsMember { get; init; } = true;

    /// <summary>Whether it is a Slack Connect conversation ('is_ext_shared').</summary>
    public bool IsExtShared { get; init; }

    /// <summary>Whether it is archived; 'exclude_archived=true' leaves it out.</summary>
    public bool IsArchived { get; init; }

    /// <summary>The topic text; empty sends none.</summary>
    public string Topic { get; init; } = string.Empty;
}
