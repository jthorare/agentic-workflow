using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Symbols;
using Aw.Context.Core;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Lenses;

public sealed class FileMapBuilderTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");
    private static readonly FileMapBasis _basis = new("v13", "7c3e1a2");

    private static readonly string[] _files =
    [
        "README.md",
        "src/billing/Invoice.ts",
        "src/billing/Tax.ts",
        "src/layout/Pane.ts",
        "src/tabs/TabGroups.ts",
        "src/tabs/TabManager.ts",
        "src/tabs/Util.ts",
    ];

    private static GraphBuilder Base() => new GraphBuilder()
        .Node("feature:tabs.tab-manager").Node("feature:tabs.tab-groups").Node("feature:layout")
        .Edge("code:src/tabs/TabManager.ts", EdgeType.Implements, "feature:tabs.tab-manager", provenance: Provenance.Deterministic)
        .Edge("code:src/tabs/TabGroups.ts", EdgeType.Implements, "feature:tabs.tab-groups", provenance: Provenance.Model, confidence: 0.74)
        .Edge("code:src/layout/Pane.ts", EdgeType.Implements, "feature:layout")
        .Edge("record:linear:issue/eng-1", EdgeType.Mentions, "feature:tabs.tab-manager")
        .Edge("decision:" + new string('a', 64), EdgeType.Decides, "feature:tabs.tab-manager");

    private static GraphSnapshot Graph() => Base().Build();

    private static FileMapView Map(string path = "src/", bool unmappedOnly = false, string[]? files = null, GraphSnapshot? graph = null) =>
        FileMapBuilder.Build(_basis, new FileMapQuery(_repo, path, unmappedOnly, Draft: false), files ?? _files, graph ?? Graph()).Value;

    private static string[] Shape(FileMapView view) => [.. view.Entries.Select(e => $"{e.Path} {e.Status.ToString().ToLowerInvariant()} {e.Files.ToString(CultureInfo.InvariantCulture)}")];

    [Fact, Trait("Lane", "unit")]
    public void The_view_echoes_the_query_and_the_basis_and_never_has_symbols()
    {
        var view = FileMapBuilder.Build(new FileMapBasis("v14", "abc1234"), new FileMapQuery(_repo, "src", UnmappedOnly: false, Draft: true), _files, Graph()).Value;

        Assert.Equal((1, "aw-split-panes", "abc1234", "v14", true, "src/"), (view.SchemaVersion, view.Repo.Value, view.Sha, view.MapVersion, view.Draft, view.Path));
        Assert.Equal((SymbolsState.Unavailable, "index not built"), (view.Symbols.State, view.Symbols.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_directory_lists_its_direct_children_with_the_worst_status_of_what_is_under_them()
    {
        var view = Map();

        Assert.Equal(["src/billing/ unmapped 2", "src/layout/ mapped 1", "src/tabs/ unmapped 3"], Shape(view));
        Assert.All(view.Entries, e => Assert.Equal(FileEntryType.Dir, e.Type));
        Assert.Equal(["billing", "layout", "tabs"], view.Entries.Select(e => e.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_subdirectory_lists_its_files_by_their_own_status()
    {
        var view = Map("src/tabs");

        Assert.Equal(["src/tabs/TabGroups.ts suggested 1", "src/tabs/TabManager.ts mapped 1", "src/tabs/Util.ts unmapped 1"], Shape(view));
        Assert.All(view.Entries, e => Assert.Equal(FileEntryType.File, e.Type));
        Assert.Equal("TabManager.ts", view.Entries[1].Name);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_root_lists_directories_before_files()
        => Assert.Equal(["src/ unmapped 6", "README.md unmapped 1"], Shape(Map("")), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_files_features_include_its_suggested_link_with_the_id_to_accept_it()
    {
        var entries = Map("src/tabs").Entries;

        var suggested = Assert.Single(entries.Single(e => e.Name == "TabGroups.ts").Features);
        Assert.Equal(("feature:tabs.tab-groups", Provenance.Model, 0.74, false, EdgeType.Implements),
            (suggested.Key.Value, suggested.Provenance, suggested.Confidence, suggested.Confirmed, suggested.Edge));
        Assert.Equal(LinkIds.For(new GraphEdge(NodeKey.From("code:src/tabs/TabGroups.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements)), suggested.LinkId);
        var mapped = Assert.Single(entries.Single(e => e.Name == "TabManager.ts").Features);
        Assert.Equal(("feature:tabs.tab-manager", Provenance.Deterministic, null, true, null), (mapped.Key.Value, mapped.Provenance, mapped.Confidence, mapped.Confirmed, mapped.LinkId));
        Assert.Empty(entries.Single(e => e.Name == "Util.ts").Features);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_directorys_features_are_only_the_confirmed_ones_its_files_implement()
    {
        var tabs = Map().Entries.Single(e => e.Name == "tabs");

        Assert.Equal(["feature:tabs.tab-manager"], tabs.Features.Select(f => f.Key.Value), StringComparer.Ordinal);
        Assert.All(tabs.Features, f => Assert.True(f.Confirmed));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_only_tests_a_feature_lists_it_with_edge_tests_and_stays_unmapped()
    {
        var graph = Base().Edge("code:src/tabs/Util.ts", EdgeType.Tests, "feature:tabs.tab-manager", provenance: Provenance.Deterministic).Build();

        var entry = Map("src/tabs", graph: graph).Entries.Single(e => e.Name == "Util.ts");

        Assert.Equal(FileMapStatus.Unmapped, entry.Status);
        var feature = Assert.Single(entry.Features);
        Assert.Equal(("feature:tabs.tab-manager", EdgeType.Tests, true), (feature.Key.Value, feature.Edge, feature.Confirmed));
    }

    [Fact, Trait("Lane", "unit")]
    public void Filemap_entry_counts_intent_on_its_confirmed_features()
    {
        var builder = Base();
        for (var i = 0; i < 5; i++)
        {
            _ = builder.Edge("record:linear:issue/groups-" + i.ToString(CultureInfo.InvariantCulture), EdgeType.Mentions, "feature:tabs.tab-groups");
        }

        var view = Map("src/", graph: builder.Build());

        var tabs = view.Entries.Single(e => e.Name == "tabs");
        Assert.Equal(new FileIntent(Records: 1, Decisions: 1), tabs.Intent);
        Assert.Equal(new FileIntent(Records: 0, Decisions: 0), view.Entries.Single(e => e.Name == "layout").Intent);
        Assert.Null(view.Entries.Single(e => e.Name == "billing").Intent);
        var groups = Map("src/tabs", graph: builder.Build()).Entries.Single(e => e.Name == "TabGroups.ts");
        Assert.Equal(new FileIntent(Records: 5, Decisions: 0), groups.Intent);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_unmapped_list_names_the_unmapped_entries_with_their_file_counts_even_when_the_entries_are_filtered()
    {
        var view = Map(unmappedOnly: true);

        Assert.Equal([("src/billing/", 2), ("src/tabs/", 3)], view.Unmapped.Select(u => (u.Path, u.Files)));
        Assert.Equal(["src/billing/", "src/tabs/"], view.Entries.Select(e => e.Path), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_totals_count_every_file_under_the_path()
    {
        Assert.Equal(new FileMapTotals(Files: 6, Mapped: 2, Suggested: 1, Unmapped: 3), Map().Totals);
        Assert.Equal(new FileMapTotals(7, 2, 1, 4), Map("").Totals);
    }

    [Fact, Trait("Lane", "unit")]
    public void Suggested_links_come_with_their_ids_the_file_path_and_the_evidence_keys()
    {
        var edge = new GraphEdge(NodeKey.From("code:src/tabs/TabGroups.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements, Provenance.Model, 0.74,
            [new EvidenceRef(RecordKey.From("linear:issue/ENG-482"))]);
        var graph = Graph() with { Edges = [.. Graph().Edges.Where(e => e.Provenance != Provenance.Model), edge] };

        var suggested = Assert.Single(Map(graph: graph).Suggested);

        Assert.Equal((LinkIds.For(edge), "src/tabs/TabGroups.ts", "feature:tabs.tab-groups", 0.74), (suggested.LinkId, suggested.File, suggested.Feature.Value, suggested.Confidence));
        Assert.Equal(["linear:issue/ENG-482"], suggested.Evidence);
        Assert.Empty(Map("src/billing").Suggested);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unmapped_only_keeps_unmapped_and_suggested_entries()
    {
        var view = Map("src/tabs", unmappedOnly: true);

        Assert.Equal(["src/tabs/TabGroups.ts suggested 1", "src/tabs/Util.ts unmapped 1"], Shape(view));
        Assert.Equal(3, view.Totals.Mapped + view.Totals.Suggested + view.Totals.Unmapped);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_path_with_no_file_under_it_is_003()
    {
        Assert.Equal(ContextErrors.UnknownAnchor.Code, FileMapBuilder.Build(_basis, new FileMapQuery(_repo, "src/nowhere/", UnmappedOnly: false, Draft: false), _files, Graph()).Error.Code);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, FileMapBuilder.Build(_basis, new FileMapQuery(_repo, "src/tabs/Util.ts", UnmappedOnly: false, Draft: false), _files, Graph()).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_with_a_confirmed_and_a_suggested_edge_is_mapped_and_still_lists_the_suggestion()
    {
        var graph = Graph() with { Edges = [.. Graph().Edges, new GraphEdge(NodeKey.From("code:src/tabs/TabManager.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements, Provenance.Model, 0.5)] };

        var view = Map("src/tabs", graph: graph);

        var manager = view.Entries.Single(e => e.Name == "TabManager.ts");
        Assert.Equal(FileMapStatus.Mapped, manager.Status);
        Assert.Equal(2, manager.Features.Count);
        Assert.Equal(2, view.Suggested.Count);
    }
}
