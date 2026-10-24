using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Judge.Core.Traits;

/// <summary>
/// A unified diff as per-file hunks and sources. Traits are judged on what changed, not on whole files: a source keeps
/// the diff markers so the question can ask about the '+' lines.
/// </summary>
public static partial class TraitDiff
{
    [GeneratedRegex(@"^//\s*trait-ignore-file\s+(?<name>\S+)\s*:\s*\S", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IgnoreLine { get; }

    /// <summary>Parses 'git diff --src-prefix=a/ --dst-prefix=b/' output. Deleted, binary and quoted-path files are skipped.</summary>
    /// <param name="diff">The diff.</param>
    /// <returns>The files that have hunks.</returns>
    public static IReadOnlyList<FileDiff> Parse(string diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        return DiffReader.Read(diff);
    }

    /// <summary>A whole file as one all-added hunk, so a scan asks the same question as a diff does.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="text">The file's text.</param>
    /// <returns>The file as a diff.</returns>
    public static FileDiff Whole(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.EndsWith('\n') ? text[..^1].Split('\n') : text.Split('\n');
        var body = new StringBuilder().Append("@@ -0,0 +1,").Append(lines.Length).Append(" @@");
        foreach (var line in lines)
        {
            _ = body.Append("\n+").Append(line);
        }

        return new FileDiff(path, [new DiffHunk(1, lines.Length, body.ToString())]);
    }

    /// <summary>Groups a file's hunks into sources of at most <paramref name="maxChars"/>, splitting an oversized hunk by lines.</summary>
    /// <param name="file">The file's diff.</param>
    /// <param name="maxChars">The most characters of one source.</param>
    /// <returns>The sources, in file order.</returns>
    public static IReadOnlyList<DiffSource> Chunk(FileDiff file, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(file);
        List<(int Start, int End, StringBuilder Text)> sources = [];
        foreach (var piece in file.Hunks.SelectMany(hunk => hunk.Text.Length <= maxChars ? [hunk] : Split(hunk, maxChars)))
        {
            if (sources.Count > 0 && sources[^1].Text.Length + piece.Text.Length + 1 <= maxChars)
            {
                var (start, _, text) = sources[^1];
                sources[^1] = (start, piece.End, text.Append('\n').Append(piece.Text));
            }
            else
            {
                sources.Add((piece.Start, piece.End, new StringBuilder(piece.Text)));
            }
        }

        return [.. sources.Select(source => new DiffSource(file.Path, source.Start, source.End, source.Text.ToString()))];
    }

    /// <summary>The traits a file opts out of: '// trait-ignore-file &lt;name&gt;: &lt;reason&gt;' in its leading comment block.</summary>
    /// <param name="head">The first characters of the file.</param>
    /// <returns>The trait names.</returns>
    public static IReadOnlySet<string> Ignored(string head)
    {
        ArgumentNullException.ThrowIfNull(head);
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (var trimmed in head.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith("#!", StringComparison.Ordinal)))
        {
            if (!trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                break;
            }

            if (IgnoreLine.Match(trimmed) is { Success: true } match)
            {
                _ = names.Add(match.Groups["name"].Value);
            }
        }

        return names;
    }

    // Each piece reports its own new-file span: ' ' and '+' lines advance the new-file line number; the '@@' header, '-'
    // and '\' lines don't. Trailing '-' lines sit past the last new-file line, so they are pinned to the hunk's end.
    private static List<DiffHunk> Split(DiffHunk hunk, int maxChars)
    {
        List<DiffHunk> pieces = [];
        List<string> lines = [];
        var size = 0;
        var next = hunk.Start;
        var pieceStart = next;

        void Flush()
        {
            var start = Math.Min(pieceStart, hunk.End);
            pieces.Add(new DiffHunk(start, Math.Max(Math.Min(next - 1, hunk.End), start), string.Join('\n', lines)));
            lines = [];
            size = 0;
            pieceStart = next;
        }

        foreach (var line in hunk.Text.Split('\n'))
        {
            if (lines.Count > 0 && size + line.Length + 1 > maxChars)
            {
                Flush();
            }

            var kept = line.Length > maxChars ? line[..maxChars] : line;
            lines.Add(kept);
            size += kept.Length + 1;
            if (line.Length > 0 && line[0] is ' ' or '+')
            {
                next++;
            }
        }

        Flush();
        return pieces;
    }
}
