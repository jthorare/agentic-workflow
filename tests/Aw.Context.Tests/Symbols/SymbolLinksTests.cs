using Aw.Context.Core.Graph;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Tests.Symbols;

[Trait("Lane", "unit")]
public sealed class SymbolLinksTests
{
    private static SymbolSite Site(string path, string name, int line) => new($"{path}#{name}", name, "method", path, line, line + 3, Signature: null, Exported: true);

    private static FileSymbolsRow Row(string path, params SymbolSite[] symbols) => new(path, symbols, Commit: "1234567", AsOfCommit: null, SymbolsState.Ok);

    private static (string Path, IReadOnlyList<NodeKey> Nodes) Link(string path, params string[] nodes) => (path, [.. nodes.Select(NodeKey.From)]);

    [Fact]
    public void A_symbol_named_like_the_nodes_last_dotted_part_implements_it()
    {
        var rows = new[] { Row("src/layout/SplitPane.ts", Site("src/layout/SplitPane.ts", "setOrientation", 12), Site("src/layout/SplitPane.ts", "render", 30)) };

        var links = SymbolLinks.For([Link("src/layout/SplitPane.ts", "surface:command/split.setOrientation")], rows);

        var link = Assert.Single(links);
        Assert.Equal(("src/layout/SplitPane.ts#setOrientation", 12, 0.9), (link.Ref, link.StartLine, link.Confidence));
    }

    [Fact]
    public void Case_and_dashes_do_not_matter_and_the_whole_tail_matches_too()
    {
        var rows = new[] { Row("src/tabs/TabManager.ts", Site("src/tabs/TabManager.ts", "TabManager", 1), Site("src/tabs/TabManager.ts", "other", 9)) };

        var links = SymbolLinks.For([Link("src/tabs/TabManager.ts", "feature:tabs.tab-manager")], rows);

        Assert.Equal(["TabManager"], links.Select(l => l.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void A_file_the_index_does_not_know_or_a_node_no_symbol_names_stays_file_level()
    {
        var rows = new[] { Row("src/a.ts", Site("src/a.ts", "unrelated", 1)) };

        var links = SymbolLinks.For([Link("src/a.ts", "feature:checkout"), Link("src/missing.ts", "feature:unrelated")], rows);

        Assert.Empty(links);
    }

    [Fact]
    public void At_most_five_symbols_per_file_and_node_in_line_order()
    {
        var symbols = Enumerable.Range(1, 8).Select(i => Site("src/a.ts", "go", i * 10)).ToArray();

        var links = SymbolLinks.For([Link("src/a.ts", "feature:go")], [Row("src/a.ts", symbols)]);

        Assert.Equal([10, 20, 30, 40, 50], links.Select(l => l.StartLine));
    }

    [Fact]
    public void Links_come_out_ordered_by_path_line_and_node()
    {
        var rows = new[] { Row("src/b.ts", Site("src/b.ts", "x", 5)), Row("src/a.ts", Site("src/a.ts", "x", 7)) };

        var links = SymbolLinks.For([Link("src/b.ts", "feature:x"), Link("src/a.ts", "feature:x")], rows);

        Assert.Equal(["src/a.ts#x", "src/b.ts#x"], links.Select(l => l.Ref), StringComparer.Ordinal);
    }
}
