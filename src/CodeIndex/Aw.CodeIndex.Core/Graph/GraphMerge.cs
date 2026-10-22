using System.Globalization;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Planning;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Graph;

/// <summary>
/// Turns a checked graph into index rows (spec §5.5): each node in a file no parser covers becomes a 'graph-symbol' symbol (no
/// signature, no clone rows, end line = its line) and each call edge a 'graphify' edge whose confidence follows graphify's word
/// (EXTRACTED is certain, anything else likely). Nodes in C# files are ignored: the syntax edges describe those.
/// </summary>
public static class GraphMerge
{
    private const string Csharp = "csharp";

    /// <summary>A node that has what a symbol needs: a file, a name and a line.</summary>
    private sealed record Placed(string NodeKey, string File, string Name, int Line);

    /// <summary>Plans the rows.</summary>
    /// <param name="repo">The repo, for the refs.</param>
    /// <param name="graph">The checked graph.</param>
    /// <param name="files">The snapshot's code files; every one that is not C# gets a record, so a file graphify no longer names loses its old symbols.</param>
    /// <returns>The file records and the edges.</returns>
    public static GraphMerged Plan(RepoName repo, GraphSnapshot graph, IReadOnlyList<SnapshotFile> files)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(files);
        var targets = files.Where(f => IsGraphLanguage(f.Path)).ToList();
        var allowed = targets.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var symbols = allowed.ToDictionary(p => p, _ => new List<SymbolRecord>(), StringComparer.Ordinal);
        var refs = Place(repo, graph, allowed, symbols);
        var (edges, dropped) = Link(graph, refs, allowed);
        var records = targets.Select(f => new FileRecord(f.Path, f.Blob, f.Size, LanguageTable.Of(f.Path) ?? "other", Parsed: false, symbols[f.Path])).ToList();
        return new GraphMerged(records, edges, dropped);
    }

    private static bool IsGraphLanguage(string path) => LanguageTable.Of(path) is { } language && !string.Equals(language, Csharp, StringComparison.Ordinal);

    // Each placeable node gets a ref (a repeat of one name in a file takes '@L<line>') and a symbol row in its file.
    private static Dictionary<string, (SymbolRef Ref, string Name)> Place(RepoName repo, GraphSnapshot graph, HashSet<string> allowed, Dictionary<string, List<SymbolRecord>> symbols)
    {
        var refs = new Dictionary<string, (SymbolRef Ref, string Name)>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes.Select(Placeable).OfType<Placed>().Where(p => allowed.Contains(p.File)))
        {
            if (Reference(repo, node, taken) is { } reference)
            {
                refs[node.NodeKey] = (reference, node.Name);
                symbols[node.File].Add(Symbol(reference, node));
            }
        }

        return refs;
    }

    private static Placed? Placeable(GraphNode node)
        => node is { File: { } file, Name: { Length: > 0 } name, Line: >= 1 and var line } ? new Placed(node.NodeKey, file, name, line) : null;

    private static (List<EdgeRecord> Edges, int Dropped) Link(GraphSnapshot graph, Dictionary<string, (SymbolRef Ref, string Name)> refs, HashSet<string> allowed)
    {
        var elsewhere = graph.Nodes.Where(n => n.File is { } f && !allowed.Contains(f)).Select(n => n.NodeKey).ToHashSet(StringComparer.Ordinal);
        var edges = new List<EdgeRecord>();
        var dropped = 0;
        foreach (var edge in graph.Edges)
        {
            if (refs.TryGetValue(edge.Source, out var from) && refs.TryGetValue(edge.Target, out var to) && edge.Line is { } line and >= 1)
            {
                var confidence = string.Equals(edge.Confidence, "EXTRACTED", StringComparison.Ordinal) ? Confidence.Certain : Confidence.Likely;
                edges.Add(new EdgeRecord(from.Ref, to.Ref, to.Name, EdgeKind.Calls, EdgeBasis.Graphify, confidence, line, Candidates: 1));
            }
            else if (!elsewhere.Contains(edge.Source))
            {
                dropped++;   // an edge from a C# file is not dropped, it is simply not this layer's: the syntax resolver owns those
            }
        }

        return (edges, dropped);
    }

    private static SymbolRef? Reference(RepoName repo, Placed node, HashSet<string> taken)
    {
        var text = string.Concat(repo.Value, ":", node.File, "#", node.Name);
        if (!taken.Add(text))
        {
            text += "@L" + node.Line.ToString(CultureInfo.InvariantCulture);
            if (!taken.Add(text))
            {
                return null;
            }
        }

        return SymbolRef.TryFrom(text, out var reference) ? reference : null;
    }

    private static SymbolRecord Symbol(SymbolRef reference, Placed node)
        => new(reference, node.Name, node.Name, SymbolKind.GraphSymbol, Arity: 0, node.Line, node.Line, Exported: false, Utility: false,
            Signature: string.Empty, ShapeHash: string.Empty, TokenCount: 0, Complexity: 0, ReadOnlyMemory<byte>.Empty, [], Body: null, []);
}
