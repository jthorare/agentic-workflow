using System.Text.RegularExpressions;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Launch.Argv;

/// <summary>Which session ids are usable (launcher L-9): the generic bound (<see cref="SessionId"/> itself), then the entry's own pattern.</summary>
public static class SessionIdRules
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(100);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Regex?> _compiled = new(StringComparer.Ordinal);

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

        return entry.SessionIdPattern is not { } pattern || Matches(pattern, value.Value);
    }

    /// <summary>Whether <paramref name="text"/> is wholly matched by an entry's session id pattern (anchored at both ends; a pattern that doesn't compile, or runs over 100 ms, matches nothing).</summary>
    /// <param name="pattern">The entry's pattern.</param>
    /// <param name="text">The id.</param>
    /// <returns>True when the whole text matches.</returns>
    public static bool Matches(string pattern, string text)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(text);
        if (Compiled(pattern) is not { } regex)
        {
            return false;
        }

        try
        {
            return regex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static Regex? Compiled(string pattern) => _compiled.GetOrAdd(pattern, static p =>
    {
        try
        {
            return new Regex(@"\A(?:" + p + @")\z", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, _timeout);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            return null;
        }
    });
}
