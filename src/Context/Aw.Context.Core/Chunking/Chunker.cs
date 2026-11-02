namespace Aw.Context.Core.Chunking;

/// <summary>
/// Windows a record's clean text into chunks of at most <see cref="WindowTokens"/> estimated tokens, each starting about
/// <see cref="OverlapTokens"/> tokens before the end of the one before (spec §5.6). Pure and deterministic.
/// </summary>
public static class Chunker
{
    /// <summary>The most estimated tokens one chunk holds.</summary>
    public const int WindowTokens = 400;
    /// <summary>The estimated tokens a chunk repeats from the end of the one before.</summary>
    public const int OverlapTokens = 40;

    /// <summary>The longest run without whitespace one unit may hold; a longer run (base64, a long url) is cut into units of this size.</summary>
    public const int MaxUnitChars = 100;

    /// <summary>The key of a record's chunk: the record key, '#', the sequence number.</summary>
    /// <param name="recordKey">The record's key.</param>
    /// <param name="seq">The chunk's sequence number.</param>
    /// <returns>The chunk key.</returns>
    public static string Key(string recordKey, int seq) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{recordKey}#{seq}");

    /// <summary>The token estimate of a text of <paramref name="words"/> words and <paramref name="chars"/> characters: the larger of words times 1.3 and characters over 3.5, rounded up.</summary>
    /// <param name="words">The words (a cut piece of a long run counts as one).</param>
    /// <param name="chars">The characters.</param>
    /// <returns>The estimate.</returns>
    public static int Estimate(int words, int chars) => (int)Math.Ceiling(Math.Max(words * 1.3, chars / 3.5));

    /// <summary>Windows <paramref name="text"/>.</summary>
    /// <param name="text">The clean text (cleaning is the caller's).</param>
    /// <returns>The chunks in order; none for empty or whitespace-only text.</returns>
    public static IReadOnlyList<TextChunk> Split(string text)
    {
        var source = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var units = Units(source);
        var chunks = new List<TextChunk>();
        var first = 0;
        while (first < units.Count)
        {
            var last = first;
            while (last + 1 < units.Count && Estimate(units, first, last + 1) <= WindowTokens)
            {
                last++;
            }

            var from = units[first];
            var to = units[last];
            chunks.Add(new TextChunk(chunks.Count, source[from.Start..to.End], Estimate(units, first, last), from.Line, to.EndLine));
            if (last == units.Count - 1)
            {
                break;
            }

            var next = last + 1;
            while (next - 1 > first && Estimate(units, next - 1, last) <= OverlapTokens)
            {
                next--;
            }

            first = next;
        }

        return chunks;
    }

    private static int Estimate(List<Unit> units, int first, int last) => Estimate(last - first + 1, units[last].End - units[first].Start);

    private static List<Unit> Units(string source)
    {
        var units = new List<Unit>();
        var line = 1;
        var i = 0;
        while (i < source.Length)
        {
            if (source[i] == '\n')
            {
                line++;
                i++;
            }
            else if (char.IsWhiteSpace(source[i]))
            {
                i++;
            }
            else
            {
                var end = i;
                while (end < source.Length && !char.IsWhiteSpace(source[end]))
                {
                    end++;
                }

                for (var start = i; start < end;)
                {
                    var cut = Math.Min(start + MaxUnitChars, end);
                    if (cut < end && char.IsHighSurrogate(source[cut - 1]))
                    {
                        cut--;
                    }

                    units.Add(new Unit(start, cut, line, line));
                    start = cut;
                }

                i = end;
            }
        }

        return units;
    }

    private sealed record Unit(int Start, int End, int Line, int EndLine);
}
