using System.Text;

namespace Aw.Sindri.Core.Messages;

/// <summary>
/// Slack's mrkdwn text, parsed into spans (messages spec §6.3, §8.1): Parse then Render gives the same text back, for any input. Bold, italic, strike,
/// inline code, code blocks, quotes, list lines, links, user and channel mentions, broadcasts and emoji names parse; unbalanced or unknown markup stays
/// text. Parse never throws on hostile text (spec §11): nesting stops at a fixed depth and every scan is bounded, so a long line stays cheap.
/// </summary>
/// <param name="Spans">The spans, in order.</param>
public sealed record Mrkdwn(IReadOnlyList<MrkdwnSpan> Spans)
{
    /// <summary>Parses Slack's mrkdwn.</summary>
    /// <param name="text">The text as Slack sends it.</param>
    /// <returns>The parsed text; one text span when the parse would not write the same text back.</returns>
    public static Mrkdwn Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return new Mrkdwn([]);
        }

        var parsed = new Mrkdwn(MrkdwnParser.Parse(text));

        // the parser writes back what it read by construction; this keeps that promise even if a rule is wrong
        return string.Equals(parsed.Render(), text, StringComparison.Ordinal) ? parsed : new Mrkdwn([new MrkdwnSpan(MrkdwnKind.Text, text)]);
    }

    /// <summary>The text to show for raw mrkdwn text: '&amp;lt;', '&amp;gt;' and '&amp;amp;' read as characters, bidi and control characters but tab and newline dropped.</summary>
    /// <param name="text">The text as Slack sends it.</param>
    /// <returns>The text to show.</returns>
    public static string ToDisplay(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var shown = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var entity = Entity(text, i);
            if (entity.Length > 0)
            {
                shown.Append(entity.Char);
                i += entity.Length - 1;
            }
            else if (IsShown(text[i]))
            {
                shown.Append(text[i]);
            }
        }

        return shown.ToString();
    }

    /// <summary>Writes the spans back as Slack's mrkdwn.</summary>
    /// <returns>The text.</returns>
    public string Render()
    {
        var text = new StringBuilder();
        foreach (var span in Spans)
        {
            span.WriteTo(text);
        }

        return text.ToString();
    }

    /// <summary>The words a reader sees, without code, URLs, mention ids or emoji names.</summary>
    /// <returns>The words.</returns>
    internal string Words()
    {
        var words = new StringBuilder();
        foreach (var span in Spans)
        {
            span.WriteWords(words);
        }

        return words.ToString();
    }

    private static (char Char, int Length) Entity(string text, int at) => text.AsSpan(at) switch
    {
        ['&', 'l', 't', ';', ..] => ('<', 4),
        ['&', 'g', 't', ';', ..] => ('>', 4),
        ['&', 'a', 'm', 'p', ';', ..] => ('&', 5),
        _ => ('\0', 0),
    };

    private static bool IsShown(char c) => c is '\n' or '\t' || !(char.IsControl(c) || IsBidi(c));

    // bidi overrides, isolates and marks can reorder what a reader sees (spec §11), so they never show
    private static bool IsBidi(char c) => c is '\u200E' or '\u200F' or '\u061C' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');
}
