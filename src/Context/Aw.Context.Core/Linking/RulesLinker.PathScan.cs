using System.Globalization;

namespace Aw.Context.Core.Linking;

/// <summary>
/// The repo-path rule's scan, by hand so it needs no clock (#3816). It finds what <c>(?&lt;![\w/.\-])(?:[\w.\-]+/)+[\w\-]+(?:\.[A-Za-z0-9]+)+(?![\w/\-])</c> finds:
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
            while (end < text.Length && InRun(text[end]))
            {
                end++;
            }

            if (PathAt(text, (i, end)) is { } stop)
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

        var ends = new List<int>();
        while (j + 1 < run.End && text[j] == '.' && char.IsAsciiLetterOrDigit(text[j + 1]))
        {
            j += 2;
            while (j < run.End && char.IsAsciiLetterOrDigit(text[j]))
            {
                j++;
            }

            ends.Add(j);
        }

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

    // The regex \w: a letter, a non-spacing mark, a decimal digit or a connector ('_'). RulesLinkerPathScanTests checks it against \w for every char.
    internal static bool IsWordChar(char c) => char.GetUnicodeCategory(c) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
        or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.NonSpacingMark or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
}
