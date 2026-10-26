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

    /// <summary>The cap for the diff of a resolution-check: a whole fix, so a judge sees all of it.</summary>
    public const int ResolutionDiffCap = 100_000;

    /// <summary>The cap for a ticket brief.</summary>
    public const int BriefCap = 8_000;

    /// <summary>The cap for a check's failure output.</summary>
    public const int FailureCap = 8_000;

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

    /// <summary>
    /// A diff cut to <paramref name="cap"/> characters of file text by whole files: files under a src/ directory first, then the
    /// rest, each in diff order, a file kept only if it still fits. When no file fits (or the diff is a single file) the first
    /// in that order is cut at a line boundary under the cap instead of dropped. Whatever was cut or dropped is named at the end,
    /// with sizes, so the reader knows it did not see everything. A diff that fits is unchanged.
    /// </summary>
    /// <param name="diff">The unified diff, one 'diff --git' section per file.</param>
    /// <param name="cap">How many characters of file text to keep.</param>
    /// <returns>The capped diff.</returns>
    public static string CappedDiff(string diff, int cap)
    {
        ArgumentNullException.ThrowIfNull(diff);
        if (diff.Length <= cap)
        {
            return diff;
        }

        var files = FileSections(diff);
        var order = Enumerable.Range(0, files.Count).OrderBy(i => IsSource(files[i].Path) ? 0 : 1).ThenBy(static i => i).ToList();
        var kept = Fitting(files, order, cap);
        int? cutIndex = kept.Count == 0 ? order[0] : null;
        var texts = files.Select((file, i) => i == cutIndex ? CutAtLine(file.Text, cap) : kept.Contains(i) ? file.Text : string.Empty).ToList();
        var notes = new List<string>();
        if (cutIndex is { } cut)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"[diff cut: {files[cut].Path} after {texts[cut].Length} of {files[cut].Text.Length} chars]"));
        }

        var dropped = files.Where((_, i) => i != cutIndex && !kept.Contains(i))
            .Select(static f => string.Create(CultureInfo.InvariantCulture, $"{f.Path} ({f.Text.Length} chars)")).ToList();
        if (dropped.Count > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"[omitted {dropped.Count} of {files.Count} files, over the {cap}-char cap: {string.Join(", ", dropped)}]"));
        }

        var body = string.Concat(texts);
        return (body.EndsWith('\n') ? body : body + "\n") + string.Join('\n', notes);
    }

    /// <summary>Renames the prompt's own delimiter tags inside the text ('&lt;/diff' becomes '&lt;/diff-text').</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with no tag that could close a block.</returns>
    public static string Neutralized(string text) => DelimiterTag.Replace(text, "<${slash}${tag}-text");

    /// <summary>The text capped at <see cref="FieldCap"/>, with every line break (and the space around it) one space.</summary>
    /// <param name="text">The text.</param>
    /// <returns>One line.</returns>
    public static string OneLine(string text) => LineBreak.Replace(Capped(text, FieldCap), " ");

    // The files, taken in priority order, that still fit whole under the cap.
    private static HashSet<int> Fitting(List<(string Path, string Text)> files, List<int> order, int cap)
    {
        var kept = new HashSet<int>();
        var used = 0;
        foreach (var index in order)
        {
            if (used + files[index].Text.Length <= cap)
            {
                _ = kept.Add(index);
                used += files[index].Text.Length;
            }
        }

        return kept;
    }

    // Up to the last line break under the cap, or the cap itself for one longer line.
    private static string CutAtLine(string text, int cap)
    {
        var lineEnd = text.LastIndexOf('\n', cap - 1);
        return text[..(lineEnd >= 0 ? lineEnd + 1 : cap)];
    }

    private static bool IsSource(string path) => path.StartsWith("src/", StringComparison.Ordinal) || path.Contains("/src/", StringComparison.Ordinal);

    // Each 'diff --git a/X b/Y' line starts a section; text before the first one belongs to the first section.
    private static List<(string Path, string Text)> FileSections(string diff)
    {
        var starts = FileStart.Matches(diff).Select(static m => m.Index).ToList();
        if (starts.Count == 0 || starts[0] != 0)
        {
            starts.Insert(0, 0);
        }

        return [.. starts.Select((start, i) =>
        {
            var text = diff[start..(i + 1 < starts.Count ? starts[i + 1] : diff.Length)];
            var header = text[..Math.Max(text.IndexOf('\n', StringComparison.Ordinal), 0)];
            var at = header.LastIndexOf(" b/", StringComparison.Ordinal);
            return (at >= 0 ? header[(at + 3)..] : header, text);
        })];
    }

    [GeneratedRegex("^diff --git ", RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FileStart { get; }

    [GeneratedRegex(@"<(?<slash>/?)(?<tag>brief|diff|problem|check-source|baseline-failure)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DelimiterTag { get; }

    [GeneratedRegex(@"\s*[\r\n\u2028\u2029]\s*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LineBreak { get; }
}
