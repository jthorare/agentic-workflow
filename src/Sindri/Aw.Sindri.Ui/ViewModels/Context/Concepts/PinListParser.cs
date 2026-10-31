using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>
/// Turns the owner's pasted list into concepts (concept pins spec §3.6 "Add several"). With no blank line every line is a concept; with blank lines every block is a concept, named by its first line
/// and defined by the lines after it, except that a line with a separator of its own (a colon, " - ", " – ") starts the next concept. Pure text work: nothing here reads the map or the profile.
/// </summary>
#pragma warning disable MA0182 // Q7b: the Add several view model and its tests use it
internal static partial class PinListParser
{
    /// <summary>The most concepts one list takes; the rest is left out.</summary>
    internal const int MaxLines = 100;

    /// <summary>The longest name a pin has.</summary>
    internal const int MaxName = 80;

    private static readonly string[] _separators = [":", " - ", " – "];

    /// <summary>Reads a pasted list.</summary>
    /// <param name="text">The box's text.</param>
    /// <returns>The concepts, at most <see cref="MaxLines"/>.</returns>
    internal static PinListRead Read(string? text)
    {
        var blocks = Blocks(text ?? string.Empty);
        var concepts = blocks.Count > 1 ? blocks.SelectMany(Concepts).ToList() : [.. blocks.SelectMany(b => b.Select(l => (IReadOnlyList<string>)[l]))];
        return new PinListRead([.. concepts.Take(MaxLines).Select((c, i) => Entry(i + 1, c))], concepts.Count > MaxLines);
    }

    // Runs of non-blank lines.
    private static List<List<string>> Blocks(string text)
    {
        var blocks = new List<List<string>>();
        List<string>? current = null;
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                current = null;
                continue;
            }

            if (current is null)
            {
                current = [];
                blocks.Add(current);
            }

            current.Add(raw.Trim());
        }

        return blocks;
    }

    // A block's concepts: its first line starts one, and each later line with a separator of its own starts the next; the others continue the concept before them.
    private static List<IReadOnlyList<string>> Concepts(List<string> block)
    {
        var concepts = new List<List<string>>();
        foreach (var line in block)
        {
            if (concepts.Count == 0 || SeparatorAt(Also.Replace(Unbulleted(line), string.Empty)) is not null)
            {
                concepts.Add([]);
            }

            concepts[^1].Add(line);
        }

        return [.. concepts];
    }

    private static PinListLine Entry(int number, IReadOnlyList<string> lines)
    {
        var text = string.Join(' ', lines);
        List<string> parts = [Unbulleted(lines[0]), .. lines.Skip(1)];
        var words = new List<string>();
        if (Also.Match(parts[^1]) is { Success: true } also)
        {
            words = [.. also.Groups["words"].Value.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
            parts[^1] = parts[^1][..also.Index].Trim();
            if (parts[^1].Length == 0)
            {
                parts.RemoveAt(parts.Count - 1);
            }
        }

        if (parts.Count == 0)
        {
            return new PinListLine(number, text, string.Empty, Definition: null, words);
        }

        if (parts.Count > 1)
        {
            return new PinListLine(number, text, parts[0], string.Join(' ', parts.Skip(1)), words);
        }

        var (name, definition) = Split(parts[0]);
        return new PinListLine(number, text, name, definition, words);
    }

    // The name is what comes before the first colon, " - ", " – " or end of sentence; the rest is the definition.
    private static (string Name, string? Definition) Split(string line)
    {
        var cut = SeparatorAt(line) is { } separator ? separator : (Index: line.Length, Length: 0);
        var name = line[..cut.Index].Trim();
        var rest = line[(cut.Index + cut.Length)..].Trim();
        return (name, rest.Length == 0 ? null : rest);
    }

    private static (int Index, int Length)? SeparatorAt(string line)
    {
        (int Index, int Length)? first = null;
        foreach (var separator in _separators)
        {
            if (line.IndexOf(separator, StringComparison.Ordinal) is var at and >= 0 && (first is null || at < first.Value.Index))
            {
                first = (at, separator.Length);
            }
        }

        return SentenceEnd.Match(line) is { Success: true } end && (first is null || end.Index < first.Value.Index) ? (end.Index, 1) : first;
    }

    private static string Unbulleted(string line) => Bullet.Replace(line, string.Empty, count: 1).Trim();

    [GeneratedRegex(@"^\s*(?:[-*•]|\d+[.)])(?:\s+|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Bullet { get; }

    [GeneratedRegex(@"\(\s*also\s*:\s*(?<words>[^)]*)\)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 100)]
    private static partial Regex Also { get; }

    [GeneratedRegex(@"[.!?](?=\s|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex SentenceEnd { get; }
}
#pragma warning restore MA0182
