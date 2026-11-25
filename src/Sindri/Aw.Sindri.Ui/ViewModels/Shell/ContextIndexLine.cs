using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// One line of the Context index card: the words, and the accessible name that leads with the card's name. The card draws the label in its own
/// column and the rest beside it, so a line that wraps at a large text scale hangs past the label instead of reading as a new row (#4269).
/// </summary>
/// <param name="Text">"linear: idle · 0 records · nothing committed yet".</param>
internal sealed partial record ContextIndexLine(string Text)
{
    private const string LabelEnd = ": ";
    private const char NoBreakSpace = ' ';
    private const char NoBreakHyphen = '‑';

    /// <summary>The line read aloud: "Context index: " first.</summary>
    public string Name { get; } = Labels.ContextIndexNamePrefix + Text;

    /// <summary>The words up to the first ": ", colon kept ("linear:"); empty when the line has none.</summary>
    public string Label { get; } = LabelOf(Text);

    /// <summary>The words after the label, with codes and time phrases glued so a wrap can't split them: "SND‑CONTEXT‑030", "1 min ago".</summary>
    public string Rest { get; } = Glue(RestOf(Text));

    /// <summary>True when the line has a label column.</summary>
    public bool HasLabel => Label.Length > 0;

    private static string LabelOf(string text)
    {
        var end = text.IndexOf(LabelEnd, StringComparison.Ordinal);
        return end <= 0 ? string.Empty : text[..(end + 1)];
    }

    private static string RestOf(string text)
    {
        var end = text.IndexOf(LabelEnd, StringComparison.Ordinal);
        return end <= 0 ? text : text[(end + LabelEnd.Length)..];
    }

    // A code's hyphens and a phrase's spaces are the only break points a wrap could take inside them; the glyphs that replace them look the same.
    private static string Glue(string rest)
    {
        var glued = Code.Replace(rest, static m => m.Value.Replace('-', NoBreakHyphen));
        glued = Phrase.Replace(glued, static m => m.Value.Replace(' ', NoBreakSpace));
        return glued;
    }

    // "SND-CONTEXT-030", "AW-DATA-005": capitals, then one or more hyphenated parts.
    [GeneratedRegex(@"\b[A-Z]+(?:-[A-Z0-9]+)+\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Code { get; }

    // The card's ages ("3 min ago", "2 h ago", "4 d ago", "under a minute ago") and "nothing committed yet".
    [GeneratedRegex(@"\b(?:\d+ (?:min|h|d)|under a minute) ago\b|\bnothing committed yet\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Phrase { get; }
}
