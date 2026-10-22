using Aw.CodeIndex.Core.Graph;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Graph;

[Trait("Lane", "unit")]
public sealed class GraphMergeTests
{
    private static readonly RepoName _repo = RepoName.From("web");

    private static readonly SnapshotFile[] _files =
    [
        new("src/a.ts", "b1", 10), new("src/b.py", "b2", 20), new("src/C.cs", "b3", 30), new("README.md", "b4", 5),
    ];

    private static GraphNode Node(string key, string file, string name, int line) => new(key, file, name, line, Community: 0);

    [Fact]
    public void Nodes_in_non_csharp_code_files_become_graph_symbols_with_the_end_line_at_the_start()
    {
        var merged = GraphMerge.Plan(_repo, new GraphSnapshot([Node("f", "src/a.ts", "f", 4), Node("c", "src/C.cs", "Cs", 2)], []), _files);

        var a = merged.Files.Single(f => f.Path == "src/a.ts");
        var symbol = Assert.Single(a.Symbols);
        Assert.Equal(("web:src/a.ts#f", SymbolKind.GraphSymbol, 4, 4, ""), (symbol.Ref.Value, symbol.Kind, symbol.StartLine, symbol.EndLine, symbol.Signature));
        Assert.False(a.Parsed);
        Assert.Equal("typescript", a.Language);
    }

    [Fact]
    public void Every_non_csharp_code_file_gets_a_record_so_a_file_graphify_forgot_loses_its_old_symbols()
    {
        var merged = GraphMerge.Plan(_repo, new GraphSnapshot([Node("f", "src/a.ts", "f", 4)], []), _files);

        Assert.Equal(["src/a.ts", "src/b.py"], merged.Files.Select(f => f.Path).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Empty(merged.Files.Single(f => f.Path == "src/b.py").Symbols);
    }

    [Fact]
    public void A_call_edge_becomes_a_graphify_edge_with_the_mapped_confidence_and_the_call_line()
    {
        var graph = new GraphSnapshot(
            [Node("f", "src/a.ts", "f", 1), Node("g", "src/a.ts", "g", 9), Node("h", "src/b.py", "h", 2)],
            [new GraphEdge("f", "g", "calls", "EXTRACTED", 3), new GraphEdge("f", "h", "calls", "INFERRED", 4)]);

        var merged = GraphMerge.Plan(_repo, graph, _files);

        var certain = merged.Edges.Single(e => e.Line == 3);
        var likely = merged.Edges.Single(e => e.Line == 4);
        Assert.Equal((EdgeKind.Calls, EdgeBasis.Graphify, Confidence.Certain, "g"), (certain.Kind, certain.Basis, certain.Confidence, certain.TargetName));
        Assert.Equal(Confidence.Likely, likely.Confidence);
        Assert.Equal("web:src/b.py#h", likely.Target!.Value.Value);
        Assert.Equal(0, merged.Dropped);
    }

    [Fact]
    public void Two_nodes_with_one_name_in_a_file_get_distinct_refs()
    {
        var merged = GraphMerge.Plan(_repo, new GraphSnapshot([Node("1", "src/a.ts", "dup", 1), Node("2", "src/a.ts", "dup", 8)], []), _files);

        var refs = merged.Files.Single(f => f.Path == "src/a.ts").Symbols.Select(s => s.Ref.Value).ToList();
        Assert.Equal(["web:src/a.ts#dup", "web:src/a.ts#dup@L8"], refs);
    }

    [Fact]
    public void An_edge_to_a_node_the_merge_could_not_place_is_counted_and_one_inside_csharp_is_not()
    {
        var graph = new GraphSnapshot(
            [Node("f", "src/a.ts", "f", 1), Node("cs", "src/C.cs", "Cs", 2)],
            [new GraphEdge("f", "gone", "calls", "EXTRACTED", 3), new GraphEdge("cs", "f", "calls", "EXTRACTED", 4)]);

        var merged = GraphMerge.Plan(_repo, graph, _files);

        Assert.Empty(merged.Edges);
        Assert.Equal(1, merged.Dropped);
    }
}
