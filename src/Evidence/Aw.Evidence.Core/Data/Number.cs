using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Evidence.Core.Data;

/// <summary>Culture-invariant text for the numbers and the interpolated strings a headline is built from.</summary>
public static partial class Number
{
    /// <summary>An integer as culture-invariant text; empty for null.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string Text(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>A string already built, so a headline can be written as an interpolated string with no implicit culture.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The same text.</returns>
    public static string Text(string text) => text;

    /// <summary>The text with each long path cut to its file name ('…/tool'), so a headline's cap keeps the reason; the full text stays in the refusal message.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The shortened text.</returns>
    public static string ShortPaths(string text) =>
        PathToken.Replace(text, static match => match.Length <= 40 ? match.Value : "…/" + match.Value[(match.Value.LastIndexOf('/') + 1)..]);

    [GeneratedRegex("""[^\s'"(),]*/[^\s'"(),]+""", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex PathToken { get; }
}
