using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Graph;

/// <summary>
/// Gives each graph symbol of a file its source text (spec §5.6): from its line up to the line before the next graph symbol of the same file, or the end of
/// the file. Pure: the caller reads the blob and supplies the scrubber, so no raw text is stored.
/// </summary>
public static class GraphBodies
{
    /// <summary>The longest raw span handed to the scrubber; the scrubber then cuts to the embed length.</summary>
    public const int RawSpanMaxCharacters = 64_000;

    /// <summary>Attaches bodies to a file's graph symbols.</summary>
    /// <param name="file">The file record with its graph symbols.</param>
    /// <param name="text">The file's text.</param>
    /// <param name="scrub">Scrubs and cuts a span; it runs on every span before it is kept.</param>
    /// <returns>The record with each symbol's scrubbed body.</returns>
    public static FileRecord Attach(FileRecord file, string text, Func<string, string> scrub)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(scrub);
        if (file.Symbols.Count == 0)
        {
            return file;
        }

        var lines = text.TrimStart('\uFEFF').Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var starts = file.Symbols.Select(s => s.StartLine).Distinct().Order().ToList();
        var symbols = file.Symbols.Select(s => s.Kind is SymbolKind.Class ? s with { Body = string.Empty } : s with { Body = scrub(Span(lines, s.StartLine, NextStart(starts, s.StartLine))) }).ToList();
        return file with { Symbols = symbols };
    }

    // The first start line after this one, or 0 when it is the last.
    private static int NextStart(List<int> starts, int line)
    {
        var index = starts.BinarySearch(line);
        return index + 1 < starts.Count ? starts[index + 1] : 0;
    }

    private static string Span(string[] lines, int startLine, int nextStart)
    {
        var first = startLine - 1;
        if (first >= lines.Length)
        {
            return string.Empty;
        }

        var end = nextStart > 0 ? Math.Min(nextStart - 1, lines.Length) : lines.Length;
        var span = string.Join('\n', lines[first..end]);
        return span.Length <= RawSpanMaxCharacters ? span : span[..RawSpanMaxCharacters];
    }
}
