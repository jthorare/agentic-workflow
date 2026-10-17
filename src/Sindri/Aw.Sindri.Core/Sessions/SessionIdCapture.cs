using System.Text.RegularExpressions;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>The fallback that finds a session id from the files the CLI wrote (launcher spec §4, 'Fallback'). Pure; 'session wrap' lists the files.</summary>
public static class SessionIdCapture
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Picks the one id among the candidate files.</summary>
    /// <param name="rule">The launch's capture rule.</param>
    /// <param name="paths">The candidate files: created after the start, within the wait.</param>
    /// <param name="claimed">Ids another launch already holds; they are not candidates.</param>
    /// <returns>The id when exactly one candidate remains; Ambiguous for more than one; None otherwise.</returns>
    public static SessionIdScan Choose(CaptureRule rule, IReadOnlyList<string> paths, IReadOnlySet<string> claimed)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(claimed);
        if (Compile(rule.Capture) is not { } capture)
        {
            return SessionIdScan.None;
        }

        var candidates = new List<SessionId>();
        foreach (var path in paths)
        {
            if (Capture(capture, path) is { } id && !claimed.Contains(id.Value) && Accepts(rule.SessionIdPattern, id) && !candidates.Contains(id))
            {
                candidates.Add(id);
            }
        }

        return candidates.Count switch
        {
            0 => SessionIdScan.None,
            1 => new SessionIdScan(candidates[0], Ambiguous: false),
            _ => new SessionIdScan(Id: null, Ambiguous: true),
        };
    }

    /// <summary>Whether an entry's session id pattern accepts <paramref name="id"/> (no pattern accepts any).</summary>
    /// <param name="pattern">The entry's pattern, or null.</param>
    /// <param name="id">The id.</param>
    /// <returns>True when it matches (run non-backtracking with a 100 ms timeout); a pattern that doesn't compile accepts nothing.</returns>
    public static bool Accepts(string? pattern, SessionId id)
    {
        if (pattern is null)
        {
            return true;
        }

        return Compile(pattern) is { } regex && Matches(regex, id.Value);
    }

    private static Regex? Compile(string? pattern)
    {
        if (pattern is null)
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, _timeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool Matches(Regex regex, string text)
    {
        try
        {
            return regex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static SessionId? Capture(Regex capture, string path)
    {
        try
        {
            var match = capture.Match(path);
            return match.Success && match.Groups.Count > 1 && SessionId.TryFrom(match.Groups[1].Value, out var id) ? id : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }
}
