namespace Aw.Sindri.Core.Commands;

/// <summary>
/// Who may read Messages text (messages spec W17, §11 "Sessions see counts"): the owner and Sindri's own services; a session, or no caller, gets ids,
/// kinds and counts only. It sits here, beside <see cref="CallerContext"/>, because Core.Messages stays pure and four read slices share it.
/// </summary>
public static class MessageReadAccess
{
    /// <summary>Whether the caller may read message text and names: the owner (human) and Sindri's own services; a session, or no caller, may not.</summary>
    /// <param name="caller">The caller of the code running now, or null.</param>
    /// <returns>True when text and names stay in the answer.</returns>
    public static bool ShowsText(CallerContext? caller) => caller is { Class: CallerClass.Human or CallerClass.Service };
}
