using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Reads one top-level task line (todo spec §4.5, D3): peels the id, the done and due markers and the links off the end, and says why the line can't be a todo when it can't.</summary>
internal static partial class NoteTodoLineReader
{
    internal const string DueMarker = "\U0001F4C5";
    internal const string DoneMarker = "\u2705";
    internal const char DoneMark = '\u2705';
    internal const string IdMarker = "^td-";
    internal const int MaxTextLength = 280;

    private const string DateFormat = "yyyy-MM-dd";
    private const int DateLength = 10;
    private const int PrefixLength = 5;

    /// <summary>True for '- [ ]', '- [x]' or '- [X]' at the start of a line, followed by a space or the end.</summary>
    /// <param name="line">The line without its ending.</param>
    /// <returns>True for a top-level task line.</returns>
    public static bool IsTaskLine(string line) =>
        line.Length >= PrefixLength
        && line.StartsWith("- [", StringComparison.Ordinal)
        && line[3] is ' ' or 'x' or 'X'
        && line[4] == ']'
        && (line.Length == PrefixLength || line[PrefixLength] == ' ');

    /// <summary>Reads a task line.</summary>
    /// <param name="number">Its line number.</param>
    /// <param name="raw">The line without its ending; <see cref="IsTaskLine"/> holds for it.</param>
    /// <returns>The line.</returns>
    public static NoteTodoLine Read(int number, string raw)
    {
        var rest = (raw.Length > PrefixLength ? raw[PrefixLength..] : string.Empty).Trim();
        rest = PeelBlock(rest, out var id, out var rejected);
        var tail = PeelTail(rest);
        var text = NoteRenderer.Unescape(tail.Text);
        var reason = Classify(tail, text, hasId: id is not null || rejected is not null);
        return new NoteTodoLine(number, raw, id, rejected, text, tail.Due, raw[3] != ' ', tail.DoneOn, tail.Links, reason);
    }

    private static string PeelBlock(string rest, out TodoId? id, out string? rejected)
    {
        id = null;
        rejected = null;
        var start = rest.AsSpan().LastIndexOfAny(" \t") + 1;
        var token = rest[start..];
        if (!token.StartsWith(IdMarker, StringComparison.Ordinal))
        {
            return rest;
        }

        if (TodoId.TryFrom("td_" + token[IdMarker.Length..], out var parsed))
        {
            id = parsed;
        }
        else
        {
            rejected = token;
        }

        return rest[..start].TrimEnd();
    }

    private static Tail PeelTail(string rest)
    {
        DateOnly? due = null;
        DateOnly? doneOn = null;
        var links = new List<string>();
        while (true)
        {
            if (doneOn is null && TryPeelDate(ref rest, DoneMarker, out var done))
            {
                doneOn = done;
            }
            else if (due is null && TryPeelDate(ref rest, DueMarker, out var when))
            {
                due = when;
            }
            else if (TryPeelLink(ref rest, out var url))
            {
                links.Insert(0, url);
            }
            else
            {
                return new Tail(rest, due, doneOn, links);
            }
        }
    }

    private static bool TryPeelDate(ref string rest, string marker, out DateOnly date)
    {
        date = default;
        var length = marker.Length + 1 + DateLength;
        if (rest.Length < length || !rest.EndsWith(marker + " " + rest[^DateLength..], StringComparison.Ordinal))
        {
            return false;
        }

        var before = rest[..^length];
        if (!DateOnly.TryParseExact(rest[^DateLength..], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || (before.Length > 0 && !char.IsWhiteSpace(before[^1])))
        {
            return false;
        }

        rest = before.TrimEnd();
        return true;
    }

    private static bool TryPeelLink(ref string rest, out string url)
    {
        var match = TrailingLink.Match(rest);
        url = match.Groups["url"].Value;
        if (!match.Success || !NoteLinks.IsTodoLink(url))
        {
            return false;
        }

        rest = rest[..match.Index].TrimEnd();
        return true;
    }

    private static NoteBreak Classify(Tail tail, string text, bool hasId)
    {
        var raw = tail.Text;
        if (hasId && raw.Contains(IdMarker, StringComparison.Ordinal))
        {
            return NoteBreak.TwoIds;
        }

        if (tail.Due is not null && DatedDue.IsMatch(raw))
        {
            return NoteBreak.TwoDueMarkers;
        }

        if (raw.Contains(IdMarker, StringComparison.Ordinal) || raw.Contains(DueMarker, StringComparison.Ordinal) || raw.Contains(DoneMarker, StringComparison.Ordinal))
        {
            return NoteBreak.MarkerInText;
        }

        return ClassifyText(raw, text);
    }

    private static NoteBreak ClassifyText(string raw, string text)
    {
        if (MarkdownLink.IsMatch(raw))
        {
            return NoteBreak.ForeignLink;
        }

        if (text.Any(IsForbidden))
        {
            return NoteBreak.ForbiddenCharacter;
        }

        return text.Length == 0 ? NoteBreak.EmptyText : text.Length > MaxTextLength ? NoteBreak.TextTooLong : NoteBreak.None;
    }

    private static bool IsForbidden(char c) =>
        char.GetUnicodeCategory(c) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    [GeneratedRegex("(?:^|(?<=\\s))\\[[^\\[\\]]*\\]\\((?<url>sindri://[^()\\s]+)\\)\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrailingLink { get; }

    [GeneratedRegex("\\[[^\\[\\]]*\\]\\([^)]*\\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MarkdownLink { get; }

    [GeneratedRegex("\U0001F4C5 [0-9]{4}-[0-9]{2}-[0-9]{2}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DatedDue { get; }

    private sealed record Tail(string Text, DateOnly? Due, DateOnly? DoneOn, IReadOnlyList<string> Links);
}
