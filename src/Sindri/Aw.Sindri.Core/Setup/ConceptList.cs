using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// Reads the owner's concept list as they have it (concept pins spec §3.6 "Add several"). Each concept is a line, or a block of lines with a blank line between blocks: the first line of a block is the name and the
/// rest its definition. When the text has no blank line at all, every line is its own concept; a block whose lines are all bullets or numbers is one concept per item. On a single line the name is the text before
/// the first ':', ' - ', an en dash with spaces, or the end of the first sentence, and the rest is the definition. Leading bullets and numbers ('-', '*', '1.') are dropped, and a trailing '(also: word, word)' gives the aliases.
/// </summary>
public static partial class ConceptList
{
    /// <summary>The most concepts one list takes; the rest are left out.</summary>
    public const int Max = 100;

    /// <summary>Reads <paramref name="text"/>.</summary>
    /// <param name="text">The list.</param>
    /// <returns>The concepts, or SND-CLI-003 naming the first line that has no name (SND-CONTEXT-177's text; nothing is written).</returns>
    public static Result<ConceptListRead> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var concepts = new List<ConceptLine>();
        foreach (var entry in Entries(lines))
        {
            var read = One(entry);
            if (read.IsError)
            {
                return read.Error;
            }

            concepts.Add(read.Value);
        }

        return new ConceptListRead([.. concepts.Take(Max)], Math.Max(0, concepts.Count - Max));
    }

    // Groups the numbered lines (blank ones dropped) into entries: one per line when the text has no blank line or the block is all bullets, else one per block.
    private static List<Entry> Entries(string[] lines)
    {
        var anyBlank = lines.Select(static l => l.Trim()).SkipWhile(static l => l.Length == 0).Reverse().SkipWhile(static l => l.Length == 0).Any(static l => l.Length == 0);
        var entries = new List<Entry>();
        var current = new List<(int Number, string Text)>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0)
            {
                Close(entries, current, anyBlank);
                continue;
            }

            current.Add((i + 1, lines[i].Trim()));
        }

        Close(entries, current, anyBlank);
        return entries;
    }

    private static void Close(List<Entry> entries, List<(int Number, string Text)> current, bool anyBlank)
    {
        if (current.Count == 0)
        {
            return;
        }

        var perLine = !anyBlank || current.TrueForAll(static l => Bullet.IsMatch(l.Text));
        entries.AddRange(perLine ? current.Select(static l => new Entry(l.Number, [l.Text])) : [new Entry(current[0].Number, [.. current.Select(static l => l.Text)])]);
        current.Clear();
    }

    private static Result<ConceptLine> One(Entry entry)
    {
        var first = Bullet.Replace(entry.Lines[0], string.Empty).Trim();
        var rest = string.Join(' ', entry.Lines.Skip(1).Select(static l => l.Trim()));
        var aliases = new List<string>();
        var tail = rest.Length > 0 ? rest : first;
        if (Also.Match(tail) is { Success: true } also)
        {
            aliases.AddRange(also.Groups["words"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            tail = tail[..also.Index].TrimEnd();
            (first, rest) = rest.Length > 0 ? (first, tail) : (tail, rest);
        }

        var (name, summary) = rest.Length > 0 ? (first.TrimEnd(':', '.'), rest) : Split(first);
        return !name.Any(char.IsLetterOrDigit)
            ? SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"line {entry.Number} has no name (fix: start the line or block with the concept's name)"))
            : new ConceptLine(name, summary.Length == 0 ? null : summary, aliases);
    }

    // The name is the text before the first ':', ' - ', an en dash with spaces, or the end of the first sentence; the rest is the definition.
    private static (string Name, string Summary) Split(string line)
    {
        var cut = Delimiter.Match(line);
        if (!cut.Success)
        {
            return (line.TrimEnd('.'), string.Empty);
        }

        return (line[..cut.Index].Trim().TrimEnd('.'), line[(cut.Index + cut.Length)..].Trim());
    }

    [GeneratedRegex(@"^\s*(?:[-*\u2022]|\d+[.)])\s+", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Bullet { get; }

    [GeneratedRegex(@"\(\s*also:\s*(?<words>[^)]*)\)\s*$", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Also { get; }

    [GeneratedRegex(@":|\s[-\u2013]\s|[.!?](?=\s)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Delimiter { get; }

    private sealed record Entry(int Number, IReadOnlyList<string> Lines);
}
