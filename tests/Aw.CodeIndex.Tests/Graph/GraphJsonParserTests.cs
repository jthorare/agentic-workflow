using System.Globalization;
using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Infrastructure.Graph;
using Aw.CodeIndex.Testing;
using Aw.CodeIndex.Tests.Parsing;

namespace Aw.CodeIndex.Tests.Graph;

[Trait("Lane", "unit")]
public sealed class GraphJsonParserTests
{
    private static readonly HashSet<string> _files = new(["src/a.ts", "src/b.ts"], StringComparer.Ordinal);

    private static Kernel.Result<Core.Model.GraphSnapshot> Parse(string json, IReadOnlySet<string>? files = null, IReadOnlyList<string>? roots = null, int maxNodes = 1_000)
        => GraphJsonParser.Parse(Encoding.UTF8.GetBytes(json), files ?? _files, roots ?? ["/snap"], maxNodes, new FakeBodyScrubber("hunter2").Scrub);

    private static string Node(string id, string label, string file = "src/a.ts", string location = "L1", string extra = "\"_callable\": true")
        => $"{{\"id\": \"{id}\", \"label\": \"{label}\", \"source_file\": \"{file}\", \"source_location\": \"{location}\", {extra}}}";

    private static string Calls(string from, string to, string confidence = "EXTRACTED", string extra = "\"source_file\": \"src/a.ts\", \"source_location\": \"L3\"")
        => $"{{\"source\": \"{from}\", \"target\": \"{to}\", \"relation\": \"calls\", \"confidence\": \"{confidence}\", {extra}}}";

    private static string Raw(string[] nodes, string[] links) => $"{{\"directed\": false, \"nodes\": [{string.Join(',', nodes)}], \"links\": [{string.Join(',', links)}]}}";

    // Twenty good nodes keep a test's few malformed records under the quarter that makes a graph untrusted.
    private static string Graph(string[] nodes, string[] links)
        => Raw([.. nodes, .. Enumerable.Range(1, 20).Select(i => Node(string.Create(CultureInfo.InvariantCulture, $"pad{i}"), string.Create(CultureInfo.InvariantCulture, $"p{i}()"), file: "src/b.ts", location: string.Create(CultureInfo.InvariantCulture, $"L{i}")))], links);

    private static List<Core.Model.GraphNode> Real(Kernel.Result<Core.Model.GraphSnapshot> parsed) => [.. parsed.Value.Nodes.Where(n => !n.NodeKey.StartsWith("pad", StringComparison.Ordinal))];

    [Fact]
    public void The_recorded_typescript_graph_yields_its_nine_symbols_and_nine_call_edges_with_lines()
    {
        var json = File.ReadAllText(Path.Combine(FixtureSupport.FixtureRoot(), "graphify", "typescript", "graph.json.recorded"));
        var files = new HashSet<string>(["src/main.ts", "src/math.ts", "src/report.ts"], StringComparer.Ordinal);

        var parsed = Parse(json, files);

        var graph = parsed.Value;
        Assert.Equal(9, graph.Nodes.Count);
        Assert.DoesNotContain(graph.Nodes, n => new[] { "src_math", "src_report", "src_main" }.Contains(n.NodeKey, StringComparer.Ordinal));
        Assert.Equal(9, graph.Edges.Count);
        Assert.Equal(2, graph.Edges.Count(e => e.Confidence == "INFERRED"));
        Assert.All(graph.Edges, e => Assert.True(e.Line >= 1));
        Assert.Contains(graph.Nodes, n => n.Name == "push" && n.File == "src/report.ts" && n.Line == 6);
    }

    [Fact]
    public void A_graph_the_pinned_graphify_wrote_inside_bubblewrap_parses_to_its_two_symbols_and_one_call()
    {
        // tests/fixtures/code-index/graphify/two-files: a.ts's f calls b.ts's g; recorded from graphify 0.9.67 run under the runner's own bwrap argv.
        var json = File.ReadAllText(Path.Combine(FixtureSupport.FixtureRoot(), "graphify", "two-files", "graph.json.recorded"));
        var files = new HashSet<string>(["src/a.ts", "src/b.ts"], StringComparer.Ordinal);

        var graph = Parse(json, files).Value;

        Assert.Equal(["f", "g"], graph.Nodes.Select(n => n.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(("src_a_f", "src_b_g", "EXTRACTED", 3), (edge.Source, edge.Target, edge.Confidence, edge.Line));
        Assert.Equal(0, graph.Dropped);
    }

    [Fact]
    public void A_wrong_top_level_shape_is_the_shape_failure()
    {
        foreach (var text in new[] { "[]", "{\"nodes\": 1, \"links\": []}", "{\"nodes\": []}", "not json" })
        {
            var parsed = Parse(text);

            Assert.True(parsed.IsError);
            Assert.Equal(CodeIndexErrors.GraphUnavailable.Code, parsed.Error.Code);
            Assert.Equal("shape", parsed.Error.Message);
        }
    }

    [Fact]
    public void A_malformed_node_is_dropped_and_counted_and_the_rest_stay()
    {
        var parsed = Parse(Graph(
            [Node("ok", "f()"), Node("bad-line", "g()", location: "12"), Node("zero", "h()", location: "L0"), Node("slash", "i()", file: "src\\\\a.ts"), Node("up", "j()", file: "../a.ts"), Node("ctl", "k\\u0007()")],
            []));

        Assert.Equal(["ok"], Real(parsed).Select(n => n.NodeKey), StringComparer.Ordinal);
        Assert.Equal(5, parsed.Value.Dropped);
    }

    [Fact]
    public void A_node_naming_a_file_outside_the_snapshot_is_dropped_and_counted()
    {
        var parsed = Parse(Graph([Node("in", "f()"), Node("out", "g()", file: "src/z.ts")], []));

        Assert.Equal(["in"], Real(parsed).Select(n => n.NodeKey), StringComparer.Ordinal);
        Assert.Equal(1, parsed.Value.Dropped);
    }

    [Fact]
    public void An_absolute_path_is_cut_at_the_snapshot_root_on_a_segment_boundary()
    {
        var parsed = Parse(Graph([Node("in", "f()", file: "/snap/src/a.ts"), Node("sibling", "g()", file: "/snap2/src/a.ts")], []));

        Assert.Equal(["src/a.ts"], Real(parsed).Select(n => n.File), StringComparer.Ordinal);
        Assert.Equal(1, parsed.Value.Dropped);
    }

    [Fact]
    public void A_repeated_id_drops_every_node_with_it()
    {
        var parsed = Parse(Graph([Node("same", "f()"), Node("same", "g()", location: "L9"), Node("other", "h()")], []));

        Assert.Equal(["other"], Real(parsed).Select(n => n.NodeKey), StringComparer.Ordinal);
        Assert.Equal(2, parsed.Value.Dropped);
    }

    [Fact]
    public void A_name_is_cleaned_of_its_brackets_and_scrubbed()
    {
        var parsed = Parse(Graph([Node("m", ".render()"), Node("c", "Report", extra: "\"_callable\": true, \"_callable_class\": true"), Node("s", "hunter2()")], []));

        Assert.Equal(["render", "Report", "[redacted]"], Real(parsed).Select(n => n.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void A_file_node_is_not_a_symbol_and_a_call_from_it_is_dropped_and_counted()
    {
        var parsed = Parse(Graph([Node("file", "a.ts", extra: "\"file_type\": \"code\""), Node("f", "f()")], [Calls("file", "f")]));

        Assert.Equal(["f"], Real(parsed).Select(n => n.NodeKey), StringComparer.Ordinal);
        Assert.Empty(parsed.Value.Edges);
        Assert.Equal(1, parsed.Value.Dropped);
    }

    [Fact]
    public void Only_calls_edges_are_kept_unknown_relations_are_skipped_and_a_call_without_a_site_is_malformed()
    {
        var parsed = Parse(Graph(
            [Node("f", "f()"), Node("g", "g()", location: "L5")],
            [
                Calls("f", "g"),
                "{\"source\": \"f\", \"target\": \"g\", \"relation\": \"contains\"}",
                "{\"source\": \"f\", \"target\": \"g\", \"relation\": \"teleports\"}",
                "{\"source\": \"g\", \"target\": \"f\", \"relation\": \"calls\", \"confidence\": \"EXTRACTED\"}",
                "{\"source\": \"g\", \"target\": \"f\"}",
            ]));

        var edge = Assert.Single(parsed.Value.Edges);
        Assert.Equal(("f", "g", 3), (edge.Source, edge.Target, edge.Line));
        Assert.Equal(2, parsed.Value.Dropped);
    }

    [Fact]
    public void A_missing_or_unknown_confidence_reads_as_inferred_and_a_repeated_pair_is_one_edge()
    {
        var parsed = Parse(Graph([Node("f", "f()"), Node("g", "g()", location: "L5")], [Calls("f", "g", confidence: "GUESS"), Calls("f", "g")]));

        Assert.Equal("INFERRED", Assert.Single(parsed.Value.Edges).Confidence);
    }

    [Fact]
    public void An_edge_naming_no_kept_node_is_dropped_and_counted()
    {
        var parsed = Parse(Graph([Node("f", "f()")], [Calls("f", "ghost")]));

        Assert.Empty(parsed.Value.Edges);
        Assert.Equal(1, parsed.Value.Dropped);
    }

    [Fact]
    public void Over_a_quarter_malformed_is_untrusted_and_over_the_node_cap_is_too_large()
    {
        var untrusted = Parse(Raw([Node("ok", "f()"), Node("a", "g()", location: "x"), Node("b", "h()", location: "y")], []));
        var large = Parse(Graph([Node("a", "f()")], []), maxNodes: 1);

        Assert.Equal("untrusted", untrusted.Error.Message);
        Assert.Equal("too-large", large.Error.Message);
    }
}
