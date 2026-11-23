using System.Globalization;

namespace Aw.Context.Core.Linking;

/// <summary>
/// The repo-path rule's scan, by hand so it needs no clock (#3816). It finds what the old pattern (?&lt;![\w/.\-])(?:[\w.\-]+/)+[\w\-]+(?:\.[A-Za-z0-9]+)+(?![\w/\-]) found:
/// a run of path chars (word chars, '/', '.', '-') can only hold one path and it starts at the run's first char; the longest set of whole folders that
/// leaves a name with one or more ASCII extensions after them wins, and a name whose last extension runs into a word char, '-' or '/' gives up that extension.
/// Each run is read a bounded number of times, so the scan is linear in the text.
/// </summary>
public static partial class RulesLinker
{
    /// <summary>The repo paths in a text, as (start, length), in text order.</summary>
    internal static List<(int Index, int Length)> RepoPaths(string text)
    {
        static bool InRun(char c) => IsWordChar(c) || c is '/' or '.' or '-';

        var found = new List<(int, int)>();
        var i = 0;
        while (i < text.Length)
        {
            if (!InRun(text[i]))
            {
                i++;
                continue;
            }

            var end = i;
            var slash = false;
            while (end < text.Length && InRun(text[end]))
            {
                slash |= text[end] == '/';
                end++;
            }

            if (slash && PathAt(text, (i, end)) is { } stop)   // why: a run with no '/' has no folder, so no path; prose allocates nothing here (#3864)
            {
                found.Add((i, stop - i));
            }

            i = end;
        }

        return found;
    }

    // The path at the start of run [Start, End), or null: each folder is one or more of [\w.\-] then '/'; the most folders that leave a matching name win.
    private static int? PathAt(string text, (int Start, int End) run)
    {
        var folders = new List<int>();
        var at = run.Start;
        while (true)
        {
            var j = at;
            while (j < run.End && text[j] != '/')
            {
                j++;
            }

            if (j == at || j == run.End)
            {
                break;
            }

            at = j + 1;
            folders.Add(at);
        }

        for (var f = folders.Count - 1; f >= 0; f--)
        {
            if (NameAt(text, (folders[f], run.End)) is { } stop)
            {
                return stop;
            }
        }

        return null;
    }

    // A name [\w\-]+ then one or more '.'-and-ASCII-alphanumerics extensions, ending where the next char is not a word char, '/' or '-'.
    private static int? NameAt(string text, (int Start, int End) run)
    {
        var j = run.Start;
        while (j < run.End && (IsWordChar(text[j]) || text[j] == '-'))
        {
            j++;
        }

        if (j == run.Start)
        {
            return null;
        }

        var ends = Extensions(text, j, run.End);
        if (ends.Count == 0)
        {
            return null;
        }

        var last = ends[^1];
        if (last == run.End || text[last] == '.')
        {
            return last;
        }

        return ends.Count > 1 ? ends[^2] : null;   // the last extension ran into a word char, '-' or '/': without it the name ends before a '.'
    }

    // Where each '.'-and-ASCII-alphanumerics extension from at ends, greedy.
    private static List<int> Extensions(string text, int at, int end)
    {
        var ends = new List<int>();
        while (at + 1 < end && text[at] == '.' && char.IsAsciiLetterOrDigit(text[at + 1]))
        {
            at += 2;
            while (at < end && char.IsAsciiLetterOrDigit(text[at]))
            {
                at++;
            }

            ends.Add(at);
        }

        return ends;
    }

    // The regex \w: a letter, a non-spacing mark, a decimal digit or a connector ('_'), as .NET's RegexCharClass.WordClass lists them (no Mc, and U+200C and U+200D only
    // in the \b boundary helper, not in \w). RulesLinkerPathScanTests checks it against \w for every char.
    internal static bool IsWordChar(char c) => char.GetUnicodeCategory(c) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
        or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.NonSpacingMark or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
}
