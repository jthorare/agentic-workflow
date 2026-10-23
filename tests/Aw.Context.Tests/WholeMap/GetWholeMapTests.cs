using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.WholeMap;

public sealed class GetWholeMapTests : IDisposable
{
    private const string AreaOrder = "area:unassigned feature:billing feature:drafted feature:tabs";
    private readonly List<MapWorld> _worlds = [];

    private static string Keys(WholeMapView view) => string.Join(' ', view.Areas.Select(a => a.Key));

    public void Dispose()
    {
        foreach (var world in _worlds)
        {
            world.Dispose();
        }
    }

    private static string Yaml(string billing = "Billing") => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        $"  - {{key: \"feature:billing\", label: \"{billing}\"}}",
        "  - {key: \"feature:billing.invoices\", label: \"Invoices\"}",
        "  - {key: \"surface:screen/invoices\", label: \"Invoices screen\"}",
        "  - {key: \"feature:tabs\", label: \"Tabs\"}",
        "  - {key: \"feature:drafted\", label: \"Drafted by a model\", provenance: model}",
        "  - {key: \"code:src/util/strings.ts\", label: \"strings.ts\"}",
        "edges:",
        "  - {from: \"feature:billing.invoices\", to: \"feature:billing\", type: part-of}",
        "  - {from: \"surface:screen/invoices\", to: \"feature:billing.invoices\", type: part-of}",
        "  - {from: \"feature:tabs\", to: \"feature:billing\", type: depends-on}");

    private async Task<MapWorld> Imported(WholeMapRules? rules = null)
    {
        var world = new MapWorld(wholeMapRules: rules);
        _worlds.Add(world);
        _ = await world.ImportYaml(Yaml());
        return world;
    }

    private static ValueTask<Kernel.Result<WholeMapView>> Ask(MapWorld world, string? detail = null, ContextScope? scope = null, string[]? locate = null, MapRead? read = null, string? repo = null, string? area = null) =>
        world.Engine.WholeMap(new WholeMapQuery(detail, locate ?? [], read ?? MapRead.Current, repo, area), scope ?? MapWorld.Human, MapWorld.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_inside_the_budget_answers_every_node_and_edge_with_its_areas()
    {
        var world = await Imported();

        var view = (await Ask(world)).Value;

        Assert.Equal((1, "v1", false, WholeMapDetail.Nodes, null), (view.SchemaVersion, view.MapVersion, view.Draft, view.Detail, view.Fallback));
        Assert.Equal((6, 3), (view.Counts.Nodes, view.Counts.Edges));
        Assert.Equal(AreaOrder, Keys(view));
        Assert.Equal(6, view.Nodes!.Count);
        Assert.Equal(3, view.Edges!.Count);
        Assert.Equal("feature:billing", view.Nodes.Single(n => n.Key.Value == "surface:screen/invoices").Area);
        var tabs = Assert.Single(view.AreaEdges);
        Assert.Equal(("feature:tabs", "feature:billing", EdgeType.DependsOn, 1), (tabs.From, tabs.To, tabs.Type, tabs.Count));
        Assert.Equal(world.Clock.GetUtcNow(), view.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_areas_name_their_members_by_kind_and_the_unassigned_group_says_so()
    {
        var world = await Imported();

        var areas = (await Ask(world)).Value.Areas;

        var billing = areas.Single(a => a.Key == "feature:billing");
        Assert.Equal((3, 2, 1), (billing.Members, billing.ByKind["feature"], billing.ByKind["surface"]));
        var loose = areas.Single(a => a.Unassigned);
        Assert.Equal((AreaIndex.UnassignedKey, AreaIndex.UnassignedLabel, 1), (loose.Key, loose.Label, loose.Members));
        Assert.Equal(AreaOrder, string.Join(' ', areas.Select(a => a.Key)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_over_the_budget_answers_areas_with_the_over_budget_fallback_even_when_nodes_were_asked()
    {
        var world = await Imported(new WholeMapRules(NodeBudget: 5));

        var auto = (await Ask(world)).Value;
        var nodes = (await Ask(world, "nodes")).Value;
        var areas = (await Ask(world, "areas")).Value;

        foreach (var v in (WholeMapView[])[auto, nodes])
        {
            Assert.Equal((WholeMapDetail.Areas, "over-budget", null, null), (v.Detail, v.Fallback, v.Nodes, v.Edges));
        }

        Assert.Equal((WholeMapDetail.Areas, null), (areas.Detail, areas.Fallback));
        Assert.Equal(6, auto.Counts.Nodes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_exactly_at_the_budget_still_answers_nodes_and_areas_can_be_asked_for_on_a_small_map()
    {
        var world = await Imported(new WholeMapRules(NodeBudget: 6));

        Assert.Equal(WholeMapDetail.Nodes, (await Ask(world)).Value.Detail);
        Assert.Null((await Ask(world, "areas")).Value.Nodes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_longer_list_of_areas_than_the_cap_is_cut_and_says_so()
    {
        var world = await Imported(new WholeMapRules(NodeBudget: 5, AreasCap: 2));

        var view = (await Ask(world)).Value;

        Assert.Equal((2, true), (view.Areas.Count, view.Truncated));
        Assert.Equal(string.Join(' ', view.Areas.Select(a => a.Key).Order(StringComparer.Ordinal)), Keys(view));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bad_detail_or_more_than_100_keys_to_place_is_006_and_100_is_fine()
    {
        var world = await Imported();

        var bad = await Ask(world, "everything");
        var hundred = await Ask(world, locate: [.. Enumerable.Range(0, 100).Select(i => "feature:n" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        var more = await Ask(world, locate: [.. Enumerable.Range(0, 101).Select(i => "feature:n" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))]);

        Assert.Equal((ContextErrors.QueryInvalid.Code, true, ContextErrors.QueryInvalid.Code), (bad.Error.Code, hundred.IsSuccess, more.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Located_names_the_area_of_each_asked_key_and_null_for_a_key_the_map_lacks()
    {
        var world = await Imported();

        var located = (await Ask(world, locate: ["surface:screen/invoices", "feature:nope", "code:src/util/strings.ts"])).Value.Located!;

        Assert.Equal([("surface:screen/invoices", "feature:billing"), ("feature:nope", null), ("code:src/util/strings.ts", AreaIndex.UnassignedKey)], located.Select(l => (l.Key, l.Area)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Located_is_left_out_when_nothing_was_asked()
    {
        var world = await Imported();

        Assert.Null((await Ask(world)).Value.Located);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_map_the_answer_is_030_and_with_no_draft_it_is_044()
    {
        var world = new MapWorld();
        _worlds.Add(world);

        var none = await Ask(world);
        _ = await world.ImportYaml(Yaml());
        var draft = await Ask(world, read: MapRead.NewestDraft);

        Assert.Equal((ContextErrors.NoMap.Code, ContextErrors.NoDraftMap.Code), (none.Error.Code, draft.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_retained_version_is_read_as_it_was_and_one_not_kept_is_120()
    {
        var world = await Imported();
        _ = await world.ImportYaml(Yaml("Billing, renamed"));

        var current = (await Ask(world)).Value;
        var first = (await Ask(world, read: MapRead.Of(draft: false, mapVersion: "v1").Value)).Value;
        var gone = await Ask(world, read: MapRead.Of(draft: false, mapVersion: "v9").Value);

        Assert.Equal(("v2", "v1", ContextErrors.MapVersionNotRetained.Code), (current.MapVersion, first.MapVersion, gone.Error.Code));
        Assert.Equal(("Billing, renamed", "Billing"), (current.Areas.Single(a => a.Key == "feature:billing").Label, first.Areas.Single(a => a.Key == "feature:billing").Label));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_at_the_cli_reads_clean_text_and_every_other_caller_gets_untrusted_labels_fenced()
    {
        var world = await Imported();

        var cli = (await Ask(world)).Value;
        var mcp = (await Ask(world, scope: MapWorld.HumanVia(ContextClient.Mcp))).Value;

        var clean = cli.Nodes!.Single(n => n.Key.Value == "feature:drafted");
        var fenced = mcp.Nodes!.Single(n => n.Key.Value == "feature:drafted");
        Assert.Equal(("Drafted by a model", Trust.Untrusted), (clean.Label, clean.Trust));
        Assert.Contains("<untrusted", fenced.Label, StringComparison.Ordinal);
        Assert.Contains("<untrusted", mcp.Areas.Single(a => a.Key == "feature:drafted").Label, StringComparison.Ordinal);
        Assert.DoesNotContain("<untrusted", cli.Areas.Single(a => a.Key == "feature:drafted").Label, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_is_refused_another_repo_and_a_repo_that_implements_nothing_is_003()
    {
        var world = await Imported();

        var other = await Ask(world, scope: MapWorld.Session("aw-split-panes"), repo: "other");
        var empty = await Ask(world, scope: MapWorld.Session("other"));

        Assert.Equal((ContextErrors.RepoOutsideScope.Code, ContextErrors.UnknownAnchor.Code), (other.Error.Code, empty.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_may_name_a_repo_of_the_profile_and_not_another()
    {
        var world = await Imported();

        var known = await Ask(world, repo: "other");
        var unknown = await Ask(world, repo: "elsewhere");

        Assert.Equal((true, ContextErrors.RepoOutsideScope.Code), (known.IsSuccess, unknown.Error.Code));
        Assert.Equal("other", known.Value.Repo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_with_one_repo_names_it_in_the_answer()
    {
        var world = await Imported();

        var view = (await Ask(world, scope: MapWorld.HumanWith(MapWorld.OnlyFixtureRepo))).Value;

        Assert.Equal("aw-split-panes", view.Repo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_question_on_the_same_map_is_answered_from_memory_and_another_caller_scope_is_not_served_it()
    {
        var world = await Imported();

        _ = await Ask(world);
        _ = await Ask(world);
        var afterRepeat = world.WholeMapBuilds;
        _ = await Ask(world, scope: MapWorld.HumanVia(ContextClient.Mcp));
        var afterOtherScope = world.WholeMapBuilds;
        _ = await Ask(world, "areas");

        Assert.Equal((1, 2, 3), (afterRepeat, afterOtherScope, world.WholeMapBuilds));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_map_version_is_never_answered_from_the_old_answer()
    {
        var world = await Imported();
        var before = (await Ask(world)).Value;

        _ = await world.ImportYaml(Yaml("Billing, renamed"));
        var after = (await Ask(world)).Value;

        Assert.Equal(("Billing", "Billing, renamed"), (before.Areas.Single(a => a.Key == "feature:billing").Label, after.Areas.Single(a => a.Key == "feature:billing").Label));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_whole_map_over_a_cached_answer_is_the_same_object_graph_of_values()
    {
        var world = await Imported();

        var first = (await Ask(world)).Value;
        var second = (await Ask(world)).Value;

        Assert.Same(first, second);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_area_opens_on_its_own_with_its_nodes_the_edges_among_them_and_the_overview_still_there()
    {
        var world = await Imported(new WholeMapRules(NodeBudget: 5));

        var view = (await Ask(world, area: "feature:billing")).Value;

        Assert.Equal(WholeMapDetail.Nodes, view.Detail);
        Assert.Null(view.Fallback);
        Assert.Equal("feature:billing feature:billing.invoices surface:screen/invoices", string.Join(' ', view.Nodes!.Select(n => n.Key.Value)));
        Assert.Equal(2, view.Edges!.Count);
        Assert.Equal(("feature:billing", false, 3, 2), (view.Area!.Key, view.Area.Truncated, view.Area.Counts.Nodes, view.Area.Counts.Edges));
        Assert.Equal((6, 4), (view.Counts.Nodes, view.Areas.Count));
        Assert.Contains(view.AreaEdges, e => e.From == "feature:tabs" && e.To == "feature:billing");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_area_over_the_cap_keeps_the_first_nodes_and_says_so_with_its_full_counts()
    {
        var world = new MapWorld(wholeMapRules: new WholeMapRules(AreaCap: 2));
        _worlds.Add(world);
        _ = await world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:wide\", label: \"Wide\"}", "  - {key: \"surface:screen/s1\", label: \"S1\"}", "  - {key: \"surface:screen/s2\", label: \"S2\"}",
            "  - {key: \"feature:a\", label: \"A\"}", "  - {key: \"feature:b\", label: \"B\"}", "  - {key: \"feature:c\", label: \"C\"}",
            "edges:", "  - {from: \"surface:screen/s1\", to: \"feature:wide\", type: part-of}", "  - {from: \"surface:screen/s2\", to: \"feature:wide\", type: part-of}"));

        var view = (await Ask(world, area: "feature:wide")).Value;

        Assert.Equal(2, view.Nodes!.Count);
        Assert.Equal((true, true, 3), (view.Area!.Truncated, view.Truncated, view.Area.Counts.Nodes));
        Assert.Equal(("feature:wide", "surface:screen/s1"), (view.Nodes[0].Key.Value, view.Nodes[1].Key.Value));
        _ = Assert.Single(view.Edges!);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_unassigned_group_opens_like_any_area_and_an_area_the_map_lacks_is_200()
    {
        var world = await Imported();

        var loose = (await Ask(world, area: AreaIndex.UnassignedKey)).Value;
        var missing = await Ask(world, area: "feature:nope");

        Assert.Equal("code:src/util/strings.ts", Assert.Single(loose.Nodes!).Key.Value);
        Assert.Equal(ContextErrors.AreaUnknown.Code, missing.Error.Code);
    }
}
