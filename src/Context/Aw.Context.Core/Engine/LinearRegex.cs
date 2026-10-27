using System.Text.RegularExpressions;

namespace Aw.Context.Core.Engine;

/// <summary>
/// Builds the regexes made from profile text (a glob turned into a pattern, a 'regex:' role entry, a recognizer pattern). They run
/// NonBacktracking, so a match reads its input once whatever the pattern looks like and needs no wall-clock limit (owner q93). A pattern
/// NonBacktracking can't take (a back-reference, a look-around, an atomic group) keeps the backtracking engine and a one-second limit; a timeout
/// there carries the pattern, which the caller names.
/// </summary>
internal static class LinearRegex
{
    private static readonly TimeSpan _backtrackingLimit = TimeSpan.FromSeconds(1);

    /// <summary>Compiles a pattern, linear when it can be.</summary>
    /// <param name="pattern">The regular expression.</param>
    /// <param name="options">The options, without NonBacktracking.</param>
    /// <returns>The regex.</returns>
    /// <exception cref="ArgumentException">The pattern does not compile.</exception>
    internal static Regex Create(string pattern, RegexOptions options)
    {
        try
        {
            return new Regex(pattern, options | RegexOptions.NonBacktracking, Regex.InfiniteMatchTimeout);
        }
        catch (NotSupportedException)
        {
            return new Regex(pattern, options, _backtrackingLimit);
        }
    }
}
