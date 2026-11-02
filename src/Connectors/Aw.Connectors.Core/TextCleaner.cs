using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Connectors.Core;

/// <summary>
/// Turns GitHub markdown, Slack mrkdwn and the HTML inside them into plain text for the index (owned index spec §5.1, "Text cleaning"):
/// comments, images and bot boilerplate are dropped, a link keeps its text and its url only when the two differ, tags go (a block tag
/// breaks the line), entities are decoded after the tags are gone, control characters go and whitespace collapses. Pure and idempotent.
/// </summary>
public static partial class TextCleaner
{
    private const int TimeoutMs = 1000;
    private const char Soft = '\u0001';
    private static readonly Dictionary<string, string> _namedEntities = new(StringComparer.Ordinal)
    {
        ["amp"] = "&",
        ["lt"] = "<",
        ["gt"] = ">",
        ["quot"] = "\"",
        ["apos"] = "'",
        ["nbsp"] = " ",
    };

    private const RegexOptions Flags = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    /// <summary>Cleans <paramref name="text"/>.</summary>
    /// <param name="text">Raw issue, pull request, comment or message text; null counts as empty.</param>
    /// <returns>Plain text with single spaces and at most one blank line between paragraphs.</returns>
    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        try
        {
            return Normalize(Decode(Markup(Strip(text.ReplaceLineEndings("\n")))));
        }
        catch (RegexMatchTimeoutException)
        {
            // Pathological input: keep the words, lose the cleaning, never fail the pull.
            return Fallback(text);
        }
    }

    private static string Strip(string text)
    {
        text = ControlChars.Replace(text, string.Empty);
        text = StandaloneComment.Replace(text, string.Empty);
        text = Comment.Replace(text, string.Empty);
        text = DependabotTail.Replace(text, string.Empty);
        text = EmailFooter.Replace(text, string.Empty);
        text = AttributionLine.Replace(text, string.Empty);
        text = MarkdownComment.Replace(text, string.Empty);
        text = MarkdownImage.Replace(text, string.Empty);
        text = StandaloneImage.Replace(text, string.Empty);
        return HtmlImage.Replace(text, string.Empty);
    }

    private static string Markup(string text)
    {
        text = SlackReference.Replace(text, SlackReferenceText);
        text = AngleLink.Replace(text, m => Link(m.Groups["text"].Success ? m.Groups["text"].Value : string.Empty, m.Groups["url"].Value));
        text = HtmlAnchor.Replace(text, m => Link(AnyTag.Replace(m.Groups["text"].Value, string.Empty), m.Groups["url"].Value));
        text = MarkdownLink.Replace(text, m => Link(m.Groups["text"].Value, m.Groups["url"].Value));
        text = FenceLine.Replace(text, string.Empty);
        text = Heading.Replace(text, string.Empty);
        text = Bold.Replace(text, "${inner}");
        text = BlockTag.Replace(text, Soft.ToString());
        return AnyTag.Replace(text, string.Empty);
    }

    private static string Decode(string text) => Entity.Replace(text, EntityText);

    private static string Normalize(string text)
    {
        // A run holding a soft break (from a block tag) is one line break, or a blank line when the source had two literal ones.
        text = SoftRun.Replace(text, m => m.Value.AsSpan().Count('\n') >= 2 ? "\n\n" : "\n");
        text = UnicodeSpaces.Replace(text, " ");
        text = HorizontalSpace.Replace(text, " ");
        var lines = text.Split('\n').Select(static l => l.Trim());
        text = string.Join('\n', lines);
        return BlankRuns.Replace(text, "\n\n").Trim();
    }

    private static string Fallback(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n').Select(static l => string.Join(' ', l.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)));
        return string.Join('\n', lines).Trim();
    }

    private static string Link(string label, string url)
    {
        label = label.Trim();
        var bare = url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? url[7..] : url;
        return label.Length == 0 || label == url || label == bare ? bare : $"{label} ({url})";
    }

    private static string SlackReferenceText(Match m)
    {
        var kind = m.Groups["kind"].Value;
        var id = m.Groups["id"].Value;
        var label = m.Groups["label"].Success ? m.Groups["label"].Value : string.Empty;
        return kind switch
        {
            "@" => label.Length > 0 ? "@" + label.TrimStart('@') : "@" + id,
            "#" => label.Length > 0 ? "#" + label.TrimStart('#') : "#" + id,
            _ => label.Length > 0 ? label : "@" + id.Split('^')[0],
        };
    }

    private static string EntityText(Match m)
    {
        var name = m.Groups["name"].Value;
        if (_namedEntities.TryGetValue(name, out var named))
        {
            return named;
        }

        var hex = name.StartsWith("#x", StringComparison.OrdinalIgnoreCase);
        var style = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        if (int.TryParse(name[(hex ? 2 : 1)..], style, CultureInfo.InvariantCulture, out var code) && code >= 32 && code != 127 && Rune.IsValid(code))
        {
            return char.ConvertFromUtf32(code);
        }

        return string.Empty;
    }

    [GeneratedRegex("[\\u0000-\\u0008\\u000B\\u000C\\u000E-\\u001F\\u007F]", Flags, TimeoutMs)]
    private static partial Regex ControlChars { get; }

    [GeneratedRegex("^[ \\t]*<!--.*?-->[ \\t]*\\n?", Flags | RegexOptions.Multiline | RegexOptions.Singleline, TimeoutMs)]
    private static partial Regex StandaloneComment { get; }

    [GeneratedRegex("<!--.*?-->", Flags | RegexOptions.Singleline, TimeoutMs)]
    private static partial Regex Comment { get; }

    // Dependabot's closing "commands and options" block, with the rule and the <details> that open it.
    [GeneratedRegex("(?:\\n[ \\t]*-{3,}[ \\t]*\\n+)?(?:<details>\\s*)?(?:<summary>\\s*)?Dependabot commands and options.*\\z", Flags | RegexOptions.Singleline, TimeoutMs)]
    private static partial Regex DependabotTail { get; }

    // The footer GitHub adds to a comment that was answered by mail.
    [GeneratedRegex("\\n(?:-- ?\\n)?[ \\t]*(?:Reply to this email directly|You are receiving this because).*\\z", Flags | RegexOptions.Singleline, TimeoutMs)]
    private static partial Regex EmailFooter { get; }

    [GeneratedRegex("^[ \\t]*(?:Co-Authored-By:|Claude-Session:|\U0001F916 Generated with).*(?:\\n|\\z)", Flags | RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeoutMs)]
    private static partial Regex AttributionLine { get; }

    [GeneratedRegex("^\\[//\\]: #.*(?:\\n|\\z)", Flags | RegexOptions.Multiline, TimeoutMs)]
    private static partial Regex MarkdownComment { get; }

    [GeneratedRegex("!\\[[^\\]\\n]*\\]\\([^)\\n]*\\)", Flags, TimeoutMs)]
    private static partial Regex MarkdownImage { get; }

    [GeneratedRegex("^[ \\t]*<img\\b[^>]*>[ \\t]*\\n?", Flags | RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeoutMs)]
    private static partial Regex StandaloneImage { get; }

    [GeneratedRegex("<img\\b[^>]*>", Flags | RegexOptions.IgnoreCase, TimeoutMs)]
    private static partial Regex HtmlImage { get; }

    // <@U1>, <@U1|name>, <#C1|general>, <!here>, <!subteam^S1|@oncall>.
    [GeneratedRegex("<(?<kind>[@#!])(?<id>[^>|\\s]*)(?:\\|(?<label>[^>]*))?>", Flags, TimeoutMs)]
    private static partial Regex SlackReference { get; }

    // <https://x>, <https://x|text> (Slack, and a markdown autolink).
    [GeneratedRegex("<(?<url>(?:https?|mailto|tel):[^>|\\s]+)(?:\\|(?<text>[^>]*))?>", Flags | RegexOptions.IgnoreCase, TimeoutMs)]
    private static partial Regex AngleLink { get; }

    [GeneratedRegex("<a\\b[^>]*?\\bhref\\s*=\\s*[\"'](?<url>[^\"']*)[\"'][^>]*>(?<text>.*?)</a\\s*>", Flags | RegexOptions.Singleline | RegexOptions.IgnoreCase, TimeoutMs)]
    private static partial Regex HtmlAnchor { get; }

    [GeneratedRegex("\\[(?<text>[^\\]\\n]*)\\]\\(\\s*<?(?<url>[^)\\s>]+)>?(?:\\s+\"[^\"]*\")?\\s*\\)", Flags, TimeoutMs)]
    private static partial Regex MarkdownLink { get; }

    [GeneratedRegex("^[ \\t]*```[^\\n]*(?:\\n|\\z)", Flags | RegexOptions.Multiline, TimeoutMs)]
    private static partial Regex FenceLine { get; }

    [GeneratedRegex("^[ \\t]{0,3}#{1,6}[ \\t]+", Flags | RegexOptions.Multiline, TimeoutMs)]
    private static partial Regex Heading { get; }

    [GeneratedRegex("\\*\\*(?<inner>[^*\\n]+)\\*\\*", Flags, TimeoutMs)]
    private static partial Regex Bold { get; }

    [GeneratedRegex("</?(?:p|div|br|li|ul|ol|tr|td|th|table|h[1-6]|details|summary|blockquote|pre|hr)\\b[^>]*>", Flags | RegexOptions.IgnoreCase, TimeoutMs)]
    private static partial Regex BlockTag { get; }

    [GeneratedRegex("</?[A-Za-z][^>]*>", Flags, TimeoutMs)]
    private static partial Regex AnyTag { get; }

    [GeneratedRegex("&(?<name>#x[0-9a-fA-F]+|#[0-9]+|amp|lt|gt|quot|apos|nbsp);", Flags, TimeoutMs)]
    private static partial Regex Entity { get; }

    [GeneratedRegex("[ \\t\\n\\u0001]*\\u0001[ \\t\\n\\u0001]*", Flags, TimeoutMs)]
    private static partial Regex SoftRun { get; }

    // U+00A0 (from &nbsp; or pasted text) and the other Unicode space separators count as a plain space.
    [GeneratedRegex("[\\u00A0\\u1680\\u2000-\\u200A\\u202F\\u205F\\u3000]", Flags, TimeoutMs)]
    private static partial Regex UnicodeSpaces { get; }

    [GeneratedRegex("[ \\t]+", Flags, TimeoutMs)]
    private static partial Regex HorizontalSpace { get; }

    [GeneratedRegex("\\n{3,}", Flags, TimeoutMs)]
    private static partial Regex BlankRuns { get; }
}
