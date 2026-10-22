using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Symbols;

/// <summary>
/// Refines file-level 'implements' links to symbols (Phase 9, pure). A symbol of the linked file implements the node when its name is the node key's
/// last segment or that segment's last dotted part, compared on letters and digits without case ('tab-manager' is TabManager, 'split.setOrientation' is setOrientation).
/// At most <see cref="MaxPerLink"/> symbols per file and node, in line order. A file or node with no such symbol stays file-level: no row, never an error.
/// </summary>
public static class SymbolLinks
{
    /// <summary>The most symbols one file-level link is refined to.</summary>
    public const int MaxPerLink = 5;

    /// <summary>The confidence of a name match.</summary>
    public const double NameMatch = 0.9;

    /// <summary>Refines the links of the files the code index knows.</summary>
    /// <param name="links">Each file with the nodes it implements.</param>
    /// <param name="rows">The index's symbols of those files.</param>
    /// <returns>The symbol-level links, ordered by path, line and node.</returns>
    public static IReadOnlyList<SymbolImplements> For(IReadOnlyList<(string Path, IReadOnlyList<NodeKey> Nodes)> links, IReadOnlyList<FileSymbolsRow> rows)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(rows);
        var byPath = rows.GroupBy(r => r.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return
        [
            .. links.Where(l => byPath.ContainsKey(l.Path))
                .SelectMany(l => l.Nodes.Distinct().SelectMany(node => Match(node, byPath[l.Path])))
                .OrderBy(s => s.Path, StringComparer.Ordinal).ThenBy(s => s.StartLine).ThenBy(s => s.Node.Value, StringComparer.Ordinal),
        ];
    }

    private static IEnumerable<SymbolImplements> Match(NodeKey node, FileSymbolsRow row)
    {
        var names = Names(node.Value);
        return row.Symbols.Where(s => names.Contains(Normalize(s.Name), StringComparer.Ordinal)).OrderBy(s => s.StartLine).Take(MaxPerLink)
            .Select(s => new SymbolImplements(node, s.Ref, s.Name, s.Path, s.StartLine, NameMatch));
    }

    private static string[] Names(string key)
    {
        var tail = key[(key.LastIndexOfAny(['/', ':']) + 1)..];
        return [.. new[] { Normalize(tail), Normalize(tail[(tail.LastIndexOf('.') + 1)..]) }.Where(n => n.Length > 0).Distinct(StringComparer.Ordinal)];
    }

    private static string Normalize(string text) => new([.. text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}
