using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The session filter of the Messages reads (messages spec W17, §11 "Sessions see counts"): a caller that is not the owner or Sindri itself gets ids,
/// kinds and counts, never message text, a display name or a conversation name. One rule for every read route, so no route forgets it.
/// </summary>
public static class MessageReadFilter
{
    /// <summary>Whether the caller may read message text and names: the owner (human) and Sindri's own services; a session, or no caller, may not.</summary>
    /// <param name="caller">The caller of the code running now, or null.</param>
    /// <returns>True when text and names stay in the answer.</returns>
    public static bool ShowsText(CallerContext? caller) => caller is { Class: CallerClass.Human or CallerClass.Service };

    /// <summary>The line without its text and author name; its ts, author id, thread and counts stay.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The line a session may read.</returns>
    public static MessageLine WithoutText(MessageLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line with
        {
            Text = string.Empty,
            AuthorName = null,
        };
    }

    /// <summary>The row without the newest message's text; its key, kind, ts and flags stay.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The row a session may read.</returns>
    public static ThreadRow WithoutText(ThreadRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row with { NewestText = string.Empty };
    }
}
