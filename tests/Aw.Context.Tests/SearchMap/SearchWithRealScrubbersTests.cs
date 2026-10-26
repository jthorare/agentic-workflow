using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Search;
using Aw.Context.Core.WholeMap;
using Aw.Context.Features.SearchMap;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Search;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.SearchMap;

/// <summary>The search store and the document cutter over the host's real scrubbers (Aw.Redaction), not the fake: the feature "Tab manager" and the file TabManager.ts must both be found by "tab manager" in the right order.</summary>
public sealed class SearchWithRealScrubbersTests
{
    private static IReadOnlyList<string> Search(string q)
    {
        var scrubbers = new RedactionScrubberFactory(new FakeTimeProvider()).For([]).Value;
        var graph = new GraphSnapshot(
            [
                new GraphNode(NodeKey.From("code:src/tabs/TabManager.ts"), "TabManager.ts", Context.Core.Results.Provenance.Deterministic),
                new GraphNode(NodeKey.From("feature:tabs.tab-manager"), "Tab manager"),
                new GraphNode(NodeKey.From("feature:layout.split-pane"), "Split pane"),
            ],
            [], [], []);
        var docs = MapSearchDocs.Of(graph, new AreaIndex(new Dictionary<string, NodeArea>(StringComparer.Ordinal)), new MapText(scrubbers, FencePolicy.Clean));
        var index = new SqliteMapSearchIndexFactory().Build([.. docs.Select(static d => d.Doc)]).Value;
        var terms = MapSearchRules.Terms(q).Value;
        return [.. index.Search(terms, area: null, limit: 10).Value.Matches.Select(m => docs[m.Doc].Node.Key.Value)];
    }

    [Fact, Trait("Lane", "unit")]
    public void The_documents_keep_the_labels_the_real_scrubbers_clean()
    {
        var scrubbers = new RedactionScrubberFactory(new FakeTimeProvider()).For([]).Value;

        Assert.Equal("Tab manager", new MapText(scrubbers, FencePolicy.Clean).Cleaned("Tab manager", singleLine: true));
    }

    [Fact, Trait("Lane", "unit")]
    public void Tab_manager_finds_the_feature_first_over_real_scrubbers()
    {
        var hits = Search("tab manager");

        Assert.Equal("feature:tabs.tab-manager", hits[0]);
    }

    // The split-panes fixture map is what the Sindri host imports. Its rename override ("Tabs") applies to the map a caller sees but not to the stored graph this second test reads.
    [Fact, Trait("Lane", "unit")]
    public async Task The_split_panes_map_finds_the_renamed_feature_by_its_new_label_and_not_by_the_old_one()
    {
        using var world = new MapWorld();
        _ = await world.ImportYaml(FixtureMap.Yaml);

        // The fixture's rename override makes the feature's label "Tabs": the search reads the map the caller sees, so "tab manager" finds only the file.
        var renamed = (await world.Engine.Search(new MapSearchQuery("tabs", Limit: null, Area: null, Repo: null, MapRead.Current), MapWorld.Human, MapWorld.None)).Value;
        var old = (await world.Engine.Search(new MapSearchQuery("tab manager", Limit: null, Area: null, Repo: null, MapRead.Current), MapWorld.Human, MapWorld.None)).Value;

        Assert.True(renamed.Hits.Count > 0 && renamed.Hits[0].Key.Value == "feature:tabs.tab-manager", string.Join(' ', renamed.Hits.Select(static h => h.Key.Value)));
        Assert.DoesNotContain(old.Hits, static h => h.Key.Value == "feature:tabs.tab-manager");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_split_panes_stored_graph_puts_the_feature_first_over_the_real_scrubbers()
    {
        using var world = new MapWorld();
        _ = await world.ImportYaml(FixtureMap.Yaml);
        var graph = (await world.Store.Snapshot(draft: false, MapWorld.None)).Value!.Graph;
        var scrubbers = new RedactionScrubberFactory(new FakeTimeProvider()).For([]).Value;

        var docs = MapSearchDocs.Of(graph, new AreaIndex(new Dictionary<string, NodeArea>(StringComparer.Ordinal)), new MapText(scrubbers, FencePolicy.Clean));
        var index = new SqliteMapSearchIndexFactory().Build([.. docs.Select(static d => d.Doc)]).Value;
        var hits = index.Search(MapSearchRules.Terms("tab manager").Value, area: null, limit: 10).Value.Matches.Select(m => docs[m.Doc].Node.Key.Value).ToList();

        Assert.True(hits.Count > 0 && hits[0] == "feature:tabs.tab-manager", string.Join(' ', docs.Select(static d => $"{d.Node.Key.Value}=[{d.Doc.Title}]")) + " => " + string.Join(' ', hits));
    }
}
