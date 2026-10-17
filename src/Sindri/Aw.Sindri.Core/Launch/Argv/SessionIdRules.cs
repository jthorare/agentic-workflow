using System.Text.RegularExpressions;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Core.Launch.Argv;

/// <summary>Which session ids are usable (launcher L-9): the generic bound (<see cref="SessionId"/> itself), then the entry's own pattern.</summary>
public static class SessionIdRules
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(100);

    /// <summary>The id when <paramref name="text"/> passes the generic bound (1 to 128 of [A-Za-z0-9._:-], no leading dot, no '..'), else null.</summary>
    /// <param name="text">Text that may be a session id.</param>
    /// <returns>The typed id, or null.</returns>
    public static SessionId? Parse(string? text) => text is not null && SessionId.TryFrom(text, out var id) ? id : null;

    /// <summary>Whether <paramref name="id"/> is present and matches the entry's pattern (run NonBacktracking with a 100 ms timeout).</summary>
    /// <param name="entry">The entry whose 'sessionIdPattern' applies; a null pattern accepts any bounded id.</param>
    /// <param name="id">The session id.</param>
    /// <returns>True when the id may be written, expanded into a template or resumed. A pattern that is invalid, unsupported by the non-backtracking engine or too slow matches nothing.</returns>
    public static bool IsUsable(AgentEntry entry, SessionId? id)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (id is not { } value)
        {
            return false;
        }

        if (entry.SessionIdPattern is not { } pattern)
        {
            return true;
        }

        try
        {
            return new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, _timeout).IsMatch(value.Value);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
