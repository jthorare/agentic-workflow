using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The map's item list (#3677, decision 3): a file row shows its full path with the directory split off to be dimmed, and the rows run by rank, not in a reading order.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapItemPathsTests
{
    private static WholeMapViewModel Map(params WholeNode[] nodes)
    {
        var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
        var draft = WholeMapLayoutTests.Draft("nodes-small");
        map.Show(draft with { Areas = [draft.Areas[0]], AreaEdges = [], Nodes = nodes, Edges = [] });
        return map;
    }

    private static WholeNode Node(string key, string label, string kind, double confidence, bool confirmed = true) =>
        new(key, label, kind, "area:x", [], "trusted", "human", confidence, confirmed);

    [Fact]
    public void A_code_file_row_carries_its_full_path_split_into_a_directory_and_a_file_name()
    {
        var map = Map(Node("code:src/ledger/Events.cs", "Events.cs", "code", 0.9));

        var item = Assert.Single(map.Items);

        Assert.Equal(("src/ledger/Events.cs", "src/ledger/", "Events.cs"), (item.Path, item.Directory, item.FileName));
    }

    [Fact]
    public void A_doc_row_takes_its_path_from_its_label_and_a_feature_row_has_no_directory()
    {
        var map = Map(Node("doc:docs/specs/ledger.md", "docs/specs/ledger.md", "decision", 0.8), Node("feature:billing", "Billing", "feature", 0.7));

        var rows = map.Items.ToDictionary(i => i.Key, StringComparer.Ordinal);

        Assert.Equal(("docs/specs/", "ledger.md"), (rows["doc:docs/specs/ledger.md"].Directory, rows["doc:docs/specs/ledger.md"].FileName));
        Assert.Equal((string.Empty, "Billing"), (rows["feature:billing"].Directory, rows["feature:billing"].FileName));
    }

    [Fact]
    public void Rows_run_confirmed_first_then_by_confidence_then_by_path()
    {
        var map = Map(
            Node("code:src/z/Zed.cs", "Zed.cs", "code", 0.5),
            Node("code:src/a/Maybe.cs", "Maybe.cs", "code", 0.99, confirmed: false),
            Node("code:src/b/Beta.cs", "Beta.cs", "code", 0.9),
            Node("code:src/a/Alpha.cs", "Alpha.cs", "code", 0.9));

        Assert.Equal(["src/a/Alpha.cs", "src/b/Beta.cs", "src/z/Zed.cs", "src/a/Maybe.cs"], [.. map.Items.Select(i => i.Path)]);
    }
}
