using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Core.Library;

/// <summary>Item ids, refs written in text, and GitHub's heading anchors.</summary>
internal static partial class DesignIds
{
    private static readonly (string Prefix, Regex Shape, DesignItemKind Kind)[] _simple =
    [
        ("REQ-", ReqShape, DesignItemKind.Requirement),
        ("AC-", AcShape, DesignItemKind.Acceptance),
        ("TP-", TpShape, DesignItemKind.TestPlan),
        ("TASK-", TaskShape, DesignItemKind.Task),
    ];

    /// <summary>Reads the id a heading, list item or table cell starts with, after an optional bold lead-in.</summary>
    /// <param name="content">The text after the marker.</param>
    /// <returns>The lead.</returns>
    internal static IdLead Lead(string content)
    {
        var s = StripBold(content);
        var end = 0;
        while (end < s.Length && (char.IsAsciiLetterOrDigit(s[end]) || s[end] == '-'))
        {
            end++;
        }

        return Classify(s[..end]);
    }

    /// <summary>Finds every id written in a text, with an optional <c language="text">doc#</c> before it.</summary>
    /// <param name="text">A cell or the rest of a key line.</param>
    /// <returns>The mentions in order.</returns>
    internal static List<IdMention> Mentions(string text)
    {
        var found = new List<IdMention>();
        foreach (Match m in Mention.Matches(text))
        {
            var lead = Classify(m.Groups["id"].Value);
            if (lead.Shape != IdShape.Valid)
            {
                continue;
            }

            var doc = m.Groups["doc"];
            found.Add(new IdMention(lead.Key, lead.Kind, doc.Success ? doc.Value + "#" + lead.Key : lead.Key));
        }

        return found;
    }

    /// <summary>The id a heading gets from its text (GitHub's rule): lowercase, markup dropped, spaces to hyphens, punctuation dropped.</summary>
    /// <param name="heading">The heading text.</param>
    /// <returns>The slug, before repeats are numbered.</returns>
    internal static string Slug(string heading)
    {
        var text = Link.Replace(heading, "${text}");
        var slug = new StringBuilder();
        foreach (var c in text.ToLowerInvariant())
        {
            if (c == ' ')
            {
                _ = slug.Append('-');
            }
            else if (c is '-' or '_' || char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)
            {
                _ = slug.Append(c);
            }
        }

        return slug.ToString();
    }

    /// <summary>Drops a bold lead-in marker.</summary>
    /// <param name="content">The text after a list marker.</param>
    /// <returns>The text without a leading <c language="text">**</c> or <c language="text">__</c>.</returns>
    internal static string StripBold(string content)
    {
        var s = content.TrimStart();
        return s.StartsWith("**", StringComparison.Ordinal) || s.StartsWith("__", StringComparison.Ordinal) ? s[2..] : s;
    }

    private static IdLead Classify(string token)
    {
        foreach (var (prefix, shape, kind) in _simple)
        {
            if (token.StartsWith(prefix, StringComparison.Ordinal))
            {
                return Whole(shape, token, kind);
            }
        }

        return token.Length > 2 && token.StartsWith("Ph", StringComparison.Ordinal) && char.IsAsciiDigit(token[2])
            ? Phased(token)
            : new IdLead(IdShape.None, DesignItemKind.Section, token);
    }

    private static IdLead Phased(string token)
    {
        var m = Phase.Match(token);
        if (!m.Success)
        {
            return new IdLead(IdShape.Bad, DesignItemKind.Phase, token);
        }

        var suffix = m.Groups["letter"].Success ? m.Groups["letter"].Value : m.Groups["sub"].Success ? "-" + NoLeadingZeros(m.Groups["sub"].Value) : string.Empty;
        return new IdLead(IdShape.Valid, DesignItemKind.Phase, "Ph" + NoLeadingZeros(m.Groups["n"].Value) + suffix);
    }

    private static IdLead Whole(Regex shape, string token, DesignItemKind kind) =>
        new(shape.IsMatch(token) ? IdShape.Valid : IdShape.Bad, kind, token);

    private static string NoLeadingZeros(string digits)
    {
        var trimmed = digits.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    [GeneratedRegex("^REQ-[UESOW]?[0-9]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReqShape { get; }

    [GeneratedRegex("^AC-[0-9]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AcShape { get; }

    [GeneratedRegex("^TP-[0-9]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TpShape { get; }

    [GeneratedRegex("^TASK-[0-9]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TaskShape { get; }

    [GeneratedRegex("^Ph(?<n>[0-9]+)(?:(?<letter>[a-z])|-(?<sub>[0-9]+))?\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Phase { get; }

    [GeneratedRegex("\\[(?<text>[^\\]]*)\\]\\([^)]*\\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Link { get; }

    [GeneratedRegex(
        "(?<![A-Za-z0-9_:/#.\\-])(?:(?<doc>doc:[^\\s#,|`()\\[\\]]+|[a-z0-9][a-z0-9-]{2,63})#)?(?<id>REQ-[UESOW]?[0-9]+|AC-[0-9]+|TP-[0-9]+|TASK-[0-9]+|Ph[0-9]+(?:[a-z]|-[0-9]+)?)(?![A-Za-z0-9_\\-])",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Mention { get; }
}
