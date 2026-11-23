using System.Text.RegularExpressions;
using Aw.Context.Core.Linear;

namespace Aw.Context.Core.Linking;

/// <summary>The linker's patterns.</summary>
public static partial class RulesLinker
{
    // No look-around, so these two run NonBacktracking: one pass over the text and no clock (owner q93), and the callers of Aliases, Matching and Significant, which read whole records, never see a timeout.
    private static Regex Word { get; } = new(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, Regex.InfiniteMatchTimeout);

    private static Regex LinearUrl { get; } = new(@"https://linear\.app/[^/\s]+/issue/(?<key>" + LinearRecordKeys.TeamKeyPattern + @"-[0-9]+)(?:/[^\s]*)?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking, Regex.InfiniteMatchTimeout);

    // These need look-around, which NonBacktracking can't run, so they keep a 1 s limit; a timeout costs only its rule (Link's Step).
    [GeneratedRegex(@"(?<![A-Za-z0-9-])" + LinearRecordKeys.TeamKeyPattern + @"-[0-9]+(?![A-Za-z0-9-])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IssueKey { get; }

    [GeneratedRegex("(?<![\\w])[\"'`“‘](?<title>[^\"'`“”‘’\\n]{2,120})[\"'`”’](?![\\w])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Quoted { get; }

    [GeneratedRegex(@"(?<![A-Za-z0-9_\-.])[A-Za-z_][A-Za-z0-9_\-.]*[A-Za-z0-9_](?![A-Za-z0-9_\-]|\.[A-Za-z0-9_])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token { get; }
}
