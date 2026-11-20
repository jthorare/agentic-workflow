using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// Scores the answer's code items and returns one page of them (#3673). A file's score adds three parts: how many words of the
/// question its path holds (a hit in the file name counts three times a hit in a folder), how near the file sits to the anchor
/// in the graph (its cascade depth: 0 is the anchor's own scope), and a big bonus when the path is the file the caller named.
/// The sum is scaled by where the file lives: code and scripts keep it, tests lose a little, docs and specs lose most.
/// </summary>
internal static partial class CodeRanker
{
    internal const int DefaultLimit = 25;

    private const double NameHit = 3;
    private const double FolderHit = 1;
    private const double NamedFile = 10;
    private const double GraphNear = 4;

    private static readonly HashSet<string> _stop = new(["the", "and", "for", "how", "does", "into", "what", "where", "who", "when", "with", "from", "that", "this", "add", "type", "wait", "slot"], StringComparer.Ordinal);

    internal static RankedPage Rank(IReadOnlyList<ResultItem> items, string? text, string? symbol, int offset, int limit)
    {
        var words = Words(text);
        var ranked = items.Select(i => Score(i, words, symbol)).OrderByDescending(s => s.Item.Score).ThenBy(s => s.Item.Node, StringComparer.Ordinal).Select(s => s.Item).ToList();
        var start = Math.Clamp(offset, 0, ranked.Count);
        var page = ranked.Skip(start).Take(Math.Max(limit, 1)).ToList();
        var next = start + page.Count;
        return new RankedPage(page, ranked.Count, next < ranked.Count ? next.ToString(CultureInfo.InvariantCulture) : null);
    }

    private static (ResultItem Item, double Value) Score(ResultItem item, IReadOnlyList<string> words, string? symbol)
    {
        var path = (item.Node ?? item.Title ?? string.Empty).Replace("code:", string.Empty, StringComparison.Ordinal);
        var slash = path.LastIndexOf('/');
        var name = Pieces(path[(slash + 1)..]);
        var folders = Pieces(slash < 0 ? string.Empty : path[..slash]);
        var nameHits = words.Where(w => name.Exists(p => Matches(p, w))).ToList();
        var folderHits = words.Where(w => !nameHits.Contains(w) && folders.Exists(p => Matches(p, w))).ToList();
        var named = !string.IsNullOrWhiteSpace(symbol) && (path.Equals(symbol, StringComparison.Ordinal) || path.EndsWith("/" + symbol, StringComparison.Ordinal));
        var near = item.Depth is { } depth ? GraphNear / (1 + depth) : GraphNear / 3;
        var raw = (nameHits.Count * NameHit) + (folderHits.Count * FolderHit) + near + (named ? NamedFile : 0);
        var score = Math.Round(raw * Weight(path), 6, MidpointRounding.ToEven);
        var ranked = item with { Score = score, Why = [Reason(path, item, nameHits, folderHits, named)] };
        return (ranked, score);
    }

    // Where the file lives: docs and specs say about the code, they are not the code.
    private static double Weight(string path) =>
        path.StartsWith("docs/", StringComparison.Ordinal) || path.StartsWith("planning/", StringComparison.Ordinal) || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? 0.4
        : path.StartsWith("tests/", StringComparison.Ordinal) || path.Contains("/tests/", StringComparison.Ordinal) ? 0.85
        : 1.0;

    private static string Reason(string path, ResultItem item, List<string> nameHits, List<string> folderHits, bool named)
    {
        if (named)
        {
            return "the file you named";
        }

        var parts = new List<string>();
        if (nameHits.Count > 0)
        {
            parts.Add("file name matches " + string.Join(", ", nameHits));
        }
        else if (folderHits.Count > 0)
        {
            parts.Add("folder matches " + string.Join(", ", folderHits));
        }

        if (item.Depth is { } depth)
        {
            parts.Add(depth == 0 ? "in the anchor's own scope" : string.Create(CultureInfo.InvariantCulture, $"{depth} {(depth == 1 ? "hop" : "hops")} from the anchor"));
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "same feature as the anchor (" + path + ")";
    }

    private static bool Matches(string piece, string word) =>
        piece.Equals(word, StringComparison.Ordinal) || (word.Length >= 4 && piece.Length >= 4 && (piece.StartsWith(word, StringComparison.Ordinal) || word.StartsWith(piece, StringComparison.Ordinal)));

    private static List<string> Words(string? text) =>
        [.. NonWord.Split(text ?? string.Empty).Select(w => w.ToLowerInvariant()).Where(w => w.Length >= 3 && !_stop.Contains(w)).Distinct(StringComparer.Ordinal)];

    // 'LaneRecycleCommand.cs' -> lane, recycle, command, cs; 'meta/lane-recycle' -> meta, lane, recycle.
    private static List<string> Pieces(string path) =>
        [.. NonWord.Split(Camel.Replace(path, "${lower} ${upper}")).Select(p => p.ToLowerInvariant()).Where(p => p.Length > 0)];

    [GeneratedRegex("[^A-Za-z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NonWord { get; }

    [GeneratedRegex("(?<lower>[a-z0-9])(?<upper>[A-Z])", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Camel { get; }
}
