using System.Text.RegularExpressions;

namespace Aw.Context.Core.Extract.Imports;

/// <summary>A cheap comment strip: block comments, and line comments that start a line or follow a space (so 'http://' in a string stays).</summary>
internal static partial class Comments
{
    internal static string Strip(string text) => Line.Replace(Block.Replace(text, " "), "");

    [GeneratedRegex("/\\*[\\s\\S]*?\\*/", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Block { get; }

    [GeneratedRegex("(?:^|\\s)//.*$", RegexOptions.CultureInvariant | RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Line { get; }
}
