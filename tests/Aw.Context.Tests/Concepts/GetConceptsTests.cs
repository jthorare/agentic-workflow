using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>The concept reads through the facade: the map is imported with its concepts as slice C will write them, and the answers are read as a human and as a session.</summary>
public sealed class GetConceptsTests : IDisposable
{
    private readonly List<MapWorld> _worlds = [];

    public void Dispose()
    {
        foreach (var world in _worlds)
        {
            world.Dispose();
        }
    }

    private static string Yaml() => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"concept:scheduling\", label: \"Self scheduling\", attributes: {summary: \"People pick and swap shifts.\", source: derived, seed: \"feature:shifts\"}, evidence: [{key: \"repo:aw-split-panes/docs/scheduling.md\"}]}",
        "  - {key: \"concept:billing\", label: \"Billing\", attributes: {summary: \"Invoices and payment.\", source: override}}",
        "  - {key: \"feature:shifts\", label: \"Shifts\"}",
        "  - {key: \"feature:invoices\", label: \"Invoices\"}",
        "  - {key: \"feature:lone\", label: \"Loose end\"}",
        "  - {key: \"code:src/shifts.ts\", label: \"shifts.ts\", provenance: deterministic}",
        "  - {key: \"code:src/invoices.ts\", label: \"invoices.ts\", provenance: deterministic}",
        "edges:",
        "  - {from: \"feature:shifts\", to: \"concept:scheduling\", type: belongs-to}",
        "  - {from: \"feature:invoices\", to: \"concept:billing\", type: belongs-to}",
        "  - {from: \"code:src/shifts.ts\", to: \"feature:shifts\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/shifts.ts\"}]}",
        "  - {from: \"code:src/invoices.ts\", to: \"feature:invoices\", type: implements, provenance: deterministic, evidence: [{key: \"repo:elsewhere/src/invoices.ts\"}]}",
        "  - {from: \"feature:invoices\", to: \"feature:shifts\", type: depends-on}");

    private async Task<MapWorld> Imported()
    {
        var world = new MapWorld();
        _worlds.Add(world);
        _ = await world.ImportYaml(Yaml());
        return world;
    }

    private static ConceptsQuery Query(string? q = null, ConceptPage? page = null) => new(MapRead.Current, page ?? new ConceptPage(), Q: q);

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_reads_every_concept_with_its_summary_size_citations_links_and_other()
    {
        var world = await Imported();

        var view = (await world.Engine.Concepts(Query(), MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal((1, "v1", ConceptStates.Ok), (view.ApiVersion, view.MapVersion, view.State));
        Assert.Equal(["concept:billing", "concept:scheduling"], view.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        var scheduling = view.Concepts.Single(c => c.Id.Value == "concept:scheduling");
        Assert.Equal(("People pick and swap shifts.", "derived", new ConceptSize(1, 0, 0, 1)), (scheduling.Summary, scheduling.Source, scheduling.Size));
        Assert.Equal(["repo:aw-split-panes/docs/scheduling.md"], scheduling.Citations.Select(c => c.Key), StringComparer.Ordinal);
        Assert.Equal(new ConceptSize(1, 0, 0, 0), view.Other.Size);
        Assert.Equal([("concept:billing", "concept:scheduling", 1)], view.Links.Select(l => (l.From.Value, l.To.Value, l.Weight)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_reads_the_concepts_of_its_own_repo_only()
    {
        var world = await Imported();

        var view = (await world.Engine.Concepts(Query(), MapWorld.Session("aw-split-panes"), MapWorld.None)).Value;

        Assert.Equal(["concept:scheduling"], view.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        Assert.Equal(new ConceptSize(0, 0, 0, 0), view.Other.Size);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_page_and_a_filter_reach_the_engine_and_a_bad_cursor_is_006()
    {
        var world = await Imported();

        var filtered = (await world.Engine.Concepts(Query("invoices"), MapWorld.Human, MapWorld.None)).Value;
        var bad = await world.Engine.Concepts(Query(page: new ConceptPage("v9:1")), MapWorld.Human, MapWorld.None);

        Assert.Equal(["concept:billing"], filtered.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        Assert.Equal(ContextErrors.QueryInvalid.Code, bad.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_that_was_never_imported_is_030()
    {
        using var world = new MapWorld();

        var view = await world.Engine.Concepts(Query(), MapWorld.Human, MapWorld.None);
        var members = await world.Engine.ConceptMembers(new ConceptMembersQuery(NodeKey.From("concept:billing"), MapRead.Current, new ConceptPage()), MapWorld.Human, MapWorld.None);

        Assert.Equal((ContextErrors.NoMap.Code, ContextErrors.NoMap.Code), (view.Error.Code, members.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_concepts_members_are_read_through_the_engine_and_an_unknown_concept_is_003()
    {
        var world = await Imported();

        var members = (await world.Engine.ConceptMembers(new ConceptMembersQuery(NodeKey.From("concept:scheduling"), MapRead.Current, new ConceptPage()), MapWorld.Human, MapWorld.None)).Value;
        var other = (await world.Engine.ConceptMembers(new ConceptMembersQuery(NodeKey.From(ConceptKeys.Other), MapRead.Current, new ConceptPage()), MapWorld.Human, MapWorld.None)).Value;
        var unknown = await world.Engine.ConceptMembers(new ConceptMembersQuery(NodeKey.From("concept:nope"), MapRead.Current, new ConceptPage()), MapWorld.Human, MapWorld.None);

        Assert.Equal([("feature:shifts", "feature", "Shifts")], members.Members.Select(m => (m.Key, m.Kind, m.Label)));
        Assert.Equal(["feature:lone"], other.Members.Select(m => m.Key), StringComparer.Ordinal);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, unknown.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_whole_map_opens_a_concept_and_the_cache_keeps_it_apart_from_the_whole_answer()
    {
        var world = await Imported();

        var concept = (await world.Engine.WholeMap(new WholeMapQuery(Detail: null, [], MapRead.Current) { Concept = "concept:scheduling" }, MapWorld.Human, MapWorld.None)).Value;
        var whole = (await world.Engine.WholeMap(new WholeMapQuery(Detail: null, [], MapRead.Current), MapWorld.Human, MapWorld.None)).Value;
        var again = (await world.Engine.WholeMap(new WholeMapQuery(Detail: null, [], MapRead.Current) { Concept = "concept:scheduling" }, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(["code:src/shifts.ts", "concept:scheduling", "feature:shifts"], concept.Nodes!.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(7, whole.Nodes!.Count);
        Assert.Equal(concept.Nodes!.Count, again.Nodes!.Count);
    }
}
