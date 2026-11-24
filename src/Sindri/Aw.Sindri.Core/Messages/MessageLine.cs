namespace Aw.Sindri.Core.Messages;

/// <summary>One message line of a conversation or thread window (messages spec §6.3, §8.2).</summary>
/// <param name="Ts">The message's ts.</param>
/// <param name="Author">The Slack user who wrote it.</param>
/// <param name="Text">Its mrkdwn text; a session's answer carries none (W17).</param>
public sealed record MessageLine(MessageTs Ts, string Author, string Text)
{
    /// <summary>Gets the thread root's ts when the line is in a thread.</summary>
    public MessageTs? Thread { get; init; }

    /// <summary>Gets the reply count of a thread root.</summary>
    public int Replies { get; init; }

    /// <summary>Gets a value indicating whether the line is known only from a search hit.</summary>
    public bool Partial { get; init; }

    /// <summary>Gets the author's display name from the users table, or null when the store has no row for them; a session's answer carries none (W17).</summary>
    public string? AuthorName { get; init; }
}
