using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The node shape and text anchors over the fixture map (spec §8.1–§8.4; the Phase 3 signal).</summary>
public sealed class GraphQueryTests : IDisposable
{
    private const string Orientation = "feature:layout.split-pane.orientation";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_node_query_returns_its_subtree_and_an_affects_section_with_paths()
    {
        _ = await _world.ImportFixture();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: Orientation), MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal([Orientation], result.Anchors, StringComparer.Ordinal);
        Assert.Equal("v1", result.MapVersion);
        Assert.Equal([Orientation, "action:split-pane.toggle-orientation", "surface:command/toggle-orientation"],
            Section(result, "graph").Items.Select(i => i.Node), StringComparer.Ordinal);
        var affects = Assert.Single(Section(result, "affects").Items);
        Assert.Equal(("feature:tabs.tab-manager", 1, "Tabs"), (affects.Node, affects.Depth, affects.Title));
        Assert.Equal([Orientation, "owns surface:command/toggle-orientation", "owns action:split-pane.toggle-orientation", "writes thing:pane-layout", "reads action:tab-manager.restore-layout"], affects.Path, StringComparer.Ordinal);
        Assert.DoesNotContain(result.Sections.SelectMany(s => s.Items), i => i.Node == "feature:layout.pane-resize");
        Assert.Equal(["code:src/layout/SplitPane.ts", "code:src/tabs/TabManager.ts"], Section(result, "code").Items.Select(i => i.Node), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_affects_item_reached_through_a_model_node_mid_path_is_model_unconfirmed_and_fenced()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1",
            "kind: authored",
            "repo: aw-split-panes",
            "nodes:",
            "  - {key: \"feature:a\", label: \"A\"}",
            "  - {key: \"surface:screen/a\", label: \"A screen\"}",
            "  - {key: \"action:a1\", label: \"Model drafted action\", provenance: model}",
            "  - {key: \"thing:t\", label: \"T\"}",
            "  - {key: \"feature:b\", label: \"B\"}",
            "  - {key: \"surface:screen/b\", label: \"B screen\"}",
            "  - {key: \"action:b1\", label: \"B action\"}",
            "edges:",
            "  - {from: \"surface:screen/a\", to: \"feature:a\", type: part-of}",
            "  - {from: \"action:a1\", to: \"surface:screen/a\", type: on}",
            "  - {from: \"action:a1\", to: \"thing:t\", type: writes}",
            "  - {from: \"action:b1\", to: \"thing:t\", type: reads}",
            "  - {from: \"action:b1\", to: \"surface:screen/b\", type: on}",
            "  - {from: \"surface:screen/b\", to: \"feature:b\", type: part-of}"));

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:a"), MapWorld.Human, MapWorld.None)).Value;

        var affects = Assert.Single(Section(result, "affects").Items);
        Assert.Equal("feature:b", affects.Node);
        Assert.Equal((Provenance.Model, false, Trust.Untrusted), (affects.Provenance, affects.Confirmed, affects.Trust));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_anchors_reaching_a_node_at_the_same_depth_show_the_path_with_the_fewest_owns_hops()
    {
        // Alpha comes first (anchors sort by key) and reaches feature:x through two owns hops; Beta reaches it through one.
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1",
            "kind: authored",
            "repo: aw-split-panes",
            "nodes:",
            "  - {key: \"feature:alpha\", label: \"Alpha\"}",
            "  - {key: \"surface:screen/alpha\", label: \"Alpha screen\"}",
            "  - {key: \"action:alpha1\", label: \"Alpha action\"}",
            "  - {key: \"thing:t\", label: \"T\"}",
            "  - {key: \"feature:beta\", label: \"Beta\"}",
            "  - {key: \"surface:screen/beta\", label: \"Beta screen\"}",
            "  - {key: \"thing:u\", label: \"U\"}",
            "  - {key: \"feature:x\", label: \"Ex\"}",
            "  - {key: \"surface:screen/x\", label: \"X screen\"}",
            "  - {key: \"action:x1\", label: \"X action\"}",
            "edges:",
            "  - {from: \"surface:screen/alpha\", to: \"feature:alpha\", type: part-of}",
            "  - {from: \"action:alpha1\", to: \"surface:screen/alpha\", type: on}",
            "  - {from: \"action:alpha1\", to: \"thing:t\", type: writes}",
            "  - {from: \"surface:screen/beta\", to: \"feature:beta\", type: part-of}",
            "  - {from: \"surface:screen/beta\", to: \"thing:u\", type: writes}",
            "  - {from: \"action:x1\", to: \"thing:t\", type: reads}",
            "  - {from: \"action:x1\", to: \"thing:u\", type: reads}",
            "  - {from: \"action:x1\", to: \"surface:screen/x\", type: on}",
            "  - {from: \"surface:screen/x\", to: \"feature:x\", type: part-of}"));

        var result = (await _world.Engine.Query(new ContextQuery("alpha beta"), MapWorld.Human, MapWorld.None)).Value;

        var affects = Assert.Single(Section(result, "affects").Items, i => i.Node == "feature:x");
        Assert.Equal(["feature:beta", "owns surface:screen/beta", "writes thing:u", "reads action:x1"], affects.Path, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_graph_item_says_where_it_came_from_and_cites_the_map_version()
    {
        _ = await _world.ImportFixture();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: Orientation), MapWorld.Human, MapWorld.None)).Value;

        foreach (var (section, item) in result.Sections.Where(s => s.Name != "records").SelectMany(s => s.Items.Select(i => (s.Name, i))))
        {
            var expected = section == "code" ? (Provenance.Deterministic, false, Trust.Trusted) : (Provenance.Human, true, Trust.Trusted);   // the fixture's implements edges are deterministic
            Assert.Equal(expected, (item.Provenance, item.Confirmed, item.Trust));
            var cite = result.Cites[Assert.Single(item.Cites)];
            Assert.Equal(("map:aw-split-panes/v1", "v1"), (cite.Key.Value, cite.Refs.Single().Version.Value));
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("feature:layout.orientation")]
    [InlineData("Split orientation")]
    public async Task An_alias_or_a_name_anchors_on_the_same_node(string node)
    {
        _ = await _world.ImportFixture();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: node), MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal([Orientation], result.Anchors);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Depth_0_answers_the_subtree_without_affects()
    {
        _ = await _world.ImportFixture();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: Orientation, Depth: 0), MapWorld.Human, MapWorld.None)).Value;

        Assert.DoesNotContain(result.Sections, s => s.Name == "affects");
        Assert.Contains(result.Sections, s => s.Name == "graph");
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, "feature:nope", "SND-CONTEXT-003")]
    [InlineData(true, Orientation, "SND-CONTEXT-030")]
    public async Task An_unknown_node_is_003_and_no_map_is_030(bool empty, string node, string code)
    {
        if (!empty)
        {
            _ = await _world.ImportFixture();
        }

        var result = await _world.Engine.Query(new ContextQuery(Text: null, Node: node), MapWorld.Human, MapWorld.None);

        Assert.Equal(code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_query_with_no_draft_map_is_030()
    {
        _ = await _world.ImportFixture();

        var result = await _world.Engine.Query(new ContextQuery(Text: null, Node: Orientation, Draft: true), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoMap.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_s_cascade_never_leaves_the_nodes_its_repo_implements()
    {
        _ = await _world.ImportFixture();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: Orientation), MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value;

        var named = result.Sections.Where(s => s.Name != "records").SelectMany(s => s.Items).Select(i => i.Node ?? string.Empty)
            .Concat(result.Sections.SelectMany(s => s.Items).SelectMany(i => i.Path ?? []).Skip(1).Select(HopTarget));
        Assert.All(named.Where(k => !k.StartsWith("code:", StringComparison.Ordinal)), k => Assert.Contains(k, FixtureMap.ScopedNodes, StringComparer.Ordinal));
        var affects = Assert.Single(Section(result, "affects").Items);   // the scope keeps the cascade's mid-path nodes (spec §3 "and their cascade"), and what it reaches is in scope too
        Assert.Equal("feature:tabs.tab-manager", affects.Node);
    }

    // A path step after the anchor is "hop key" ("owns surface:x", "writes thing:t", "reads action:a"): the target is what a leak check names.
    private static string HopTarget(string step) => step[(step.IndexOf(' ', StringComparison.Ordinal) + 1)..];

    [Fact, Trait("Lane", "unit")]
    public async Task A_question_naming_a_node_anchors_on_it_and_still_cites_repo_docs()
    {
        _ = await _world.ImportFixture();

        var result = (await _world.Engine.Query(new ContextQuery("What else is affected if I change how split orientation works?"), MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal([Orientation], result.Anchors);
        _ = Assert.Single(Section(result, "affects").Items);
        Assert.NotEmpty(Section(result, "records").Items);
        Assert.Contains("surface:command/toggle-orientation", result.SuggestedAnchors, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_map_a_question_is_answered_as_in_phase_1()
    {
        var result = (await _world.Engine.Query(new ContextQuery("orientation toggle"), MapWorld.Human, MapWorld.None)).Value;

        Assert.Empty(result.Anchors);
        Assert.Null(result.MapVersion);
        Assert.Equal(["records"], result.Sections.Select(s => s.Name), StringComparer.Ordinal);
    }

    private static ResultSection Section(ContextResult result, string name) => result.Sections.Single(s => s.Name == name);
}
