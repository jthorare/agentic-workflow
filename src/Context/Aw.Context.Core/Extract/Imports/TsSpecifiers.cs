using System.Text.RegularExpressions;

namespace Aw.Context.Core.Extract.Imports;

/// <summary>The string-literal module specifiers in a TS or JS file: import and export 'from', bare import, require and dynamic import.</summary>
internal static partial class TsSpecifiers
{
    internal static IReadOnlyList<string> Read(string text)
    {
        try
        {
            return [.. Specifier.Matches(Comments.Strip(text)).Select(m => m.Groups["s"].Value)];
        }
        catch (RegexMatchTimeoutException)
        {
            return [];   // why: a file that defeats the pattern gives no edges rather than failing the map
        }
    }

    [GeneratedRegex("(?:\\bfrom\\s*|\\bimport\\s*\\(?\\s*|\\brequire\\s*\\(\\s*)(?<q>[\"'])(?<s>[^\"'\\r\\n]+)\\k<q>", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Specifier { get; }
}
