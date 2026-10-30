using Aw.Context.Core.Engine;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.QueryContext;

/// <summary>Concept pins spec §3.2: <c xml:lang="en">concept=</c> on the request, and the lines an answer says about it.</summary>
public sealed class ConceptQueryTests : IDisposable
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string Invoices = "feature:billing.invoices";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Scope(params string[] pins) =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo,
        [
            new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes")
            {
                Concepts = [.. pins.Select(p => ConceptPin.Of(ConceptId.From(p), p, summary: null, features: null, paths: null))],
            },
        ]);

    private async Task Import() =>
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"concept:tabs\", label: \"Tabs\"}",
            "  - {key: \"concept:billing\", label: \"Billing\"}",
            "  - {key: \"feature:tabs.tab-manager\", label: \"Tab manager\"}",
            "  - {key: \"feature:billing.invoices\", label: \"Invoices\"}",
            "edges:",
            "  - {from: \"feature:tabs.tab-manager\", to: \"concept:tabs\", type: belongs-to}",
            "  - {from: \"feature:billing.invoices\", to: \"concept:billing\", type: belongs-to}"));

    [Fact, Trait("Lane", "unit")]
    public async Task Context_query_takes_concept_and_answers_scoped()
    {
        await Import();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: Manager) { Concept = "tabs" }, Scope("tabs", "billing"), MapWorld.None)).Value;

        Assert.Equal("tabs", result.Query.Concept);
        Assert.Equal(["scoped to concept tabs (map v1)"], result.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_anchor_in_another_pin_is_told_and_the_filter_wins()
    {
        await Import();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: Invoices) { Concept = "tabs" }, Scope("tabs", "billing"), MapWorld.None)).Value;

        Assert.Equal(["scoped to concept tabs (map v1)", "anchor concept:billing is outside concept tabs, so its lens shows only tabs's part"], result.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_name_that_is_no_pin_is_102_and_a_blank_one_is_006()
    {
        await Import();

        var typo = await _world.Engine.Query(new ContextQuery(Text: null, Node: Manager) { Concept = "tabz" }, Scope("tabs", "billing"), MapWorld.None);
        var blank = await _world.Engine.Query(new ContextQuery(Text: null, Node: Manager) { Concept = " " }, Scope("tabs", "billing"), MapWorld.None);

        Assert.Equal("SND-CONTEXT-102", typo.Error.Code);
        Assert.Equal("SND-CONTEXT-006", blank.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_text_question_that_anchors_on_a_pin_hints_at_concept()
    {
        await Import();

        var result = (await _world.Engine.Query(new ContextQuery("Tabs"), Scope("tabs", "billing"), MapWorld.None)).Value;

        Assert.Equal(["anchored on concept:tabs; add concept=tabs to see only tabs"], result.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_pin_in_play_there_are_no_notes()
    {
        await Import();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: Manager), Scope(), MapWorld.None)).Value;

        Assert.Null(result.Notes);
    }
}
