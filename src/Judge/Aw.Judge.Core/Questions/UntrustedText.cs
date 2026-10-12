using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// How a prompt carries text it didn't write (a ticket, a diff, a problem statement): capped so one huge field can't blow
/// the provider's prompt budget, its delimiter tags defused so it can't close its own block early, and labelled fields
/// kept on one line so a field can't start a fake trusted line of its own.
/// </summary>
public static partial class UntrustedText
{
    /// <summary>The cap for a labelled one-line field.</summary>
    public const int FieldCap = 2_000;

    /// <summary>The cap for a diff.</summary>
    public const int DiffCap = 12_000;

    /// <summary>The cap for a ticket brief.</summary>
    public const int BriefCap = 8_000;

    /// <summary>The text cut to <paramref name="cap"/> characters with a visible "[truncated N chars]" line, or unchanged when it fits.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cap">How many characters to keep.</param>
    /// <returns>The capped text.</returns>
    public static string Capped(string text, int cap)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length <= cap
            ? text
            : string.Create(CultureInfo.InvariantCulture, $"{text[..cap]}\n[truncated {text.Length - cap} chars]");
    }

    /// <summary>Renames the prompt's own delimiter tags inside the text ('&lt;/diff' becomes '&lt;/diff-text').</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with no tag that could close a block.</returns>
    public static string Neutralized(string text) => DelimiterTag.Replace(text, "<${slash}${tag}-text");

    /// <summary>The text capped at <see cref="FieldCap"/>, with every line break (and the space around it) one space.</summary>
    /// <param name="text">The text.</param>
    /// <returns>One line.</returns>
    public static string OneLine(string text) => LineBreak.Replace(Capped(text, FieldCap), " ");

    [GeneratedRegex(@"<(?<slash>/?)(?<tag>brief|diff|problem)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DelimiterTag { get; }

    [GeneratedRegex(@"\s*[\r\n\u2028\u2029]\s*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LineBreak { get; }
}
