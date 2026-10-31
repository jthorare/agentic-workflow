using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Features.QueryContext;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.QueryContext;

/// <summary>Concept pins spec §3.1: the pins of a query are resolved once, before any leg runs.</summary>
public sealed class PinContextTests
{
    private static readonly ConceptPin _billing = ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: null, paths: ["services/billing/**"]);

    private static ContextRepo Repo(params ConceptPin[] pins) => new(ContextRepoName.From("api"), "/api") { Concepts = pins };

    private static VisibleMap Map(string? ruleset)
    {
        var graph = new GraphBuilder()
            .Edge("code:services/billing/charge.cs", EdgeType.Implements, "feature:api.services.billing", provenance: Provenance.Deterministic)
            .Edge("feature:api.services.billing", EdgeType.BelongsTo, "concept:billing")
            .Build();
        var version = new MapVersion(41, ContextRepoName.From("api"), Commit: null, KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Current) { Ruleset = ruleset };
        return new VisibleMap(new MapSnapshot(version, graph, []), graph);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_pins_and_no_concept_nothing_is_resolved()
    {
        var api = Repo();

        var pins = PinContext.Of([api], [api], Map(ruleset: null), concept: null, ["feature:api.services.billing"]);

        Assert.Same(PinContext.None, pins.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_anchors_pins_are_found_once_each()
    {
        var api = Repo(_billing);

        var pins = PinContext.Of([api], [api], Map(MapRuleset.Of([api])), concept: null, ["feature:api.services.billing", "code:services/billing/charge.cs", "feature:elsewhere"]);

        Assert.Equal("billing", Assert.Single(pins.Value.AnchorPins).Value);
        Assert.Null(pins.Value.StaleNote);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_concept_with_no_readable_map_is_105()
    {
        var api = Repo(_billing);

        var error = PinContext.Of([api], [api], map: null, concept: "billing", []).Error;

        Assert.Equal("SND-CONTEXT-105", error.Code);
        Assert.Equal("concept= needs a built map (fix: sindri context map build)", error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_concept_names_its_scope_and_an_unknown_one_is_102()
    {
        var api = Repo(_billing);

        var named = PinContext.Of([api], [api], Map(MapRuleset.Of([api])), concept: "billing", []);
        var typo = PinContext.Of([api], [api], Map(MapRuleset.Of([api])), concept: "bilin", []);

        Assert.Equal("billing", named.Value.Scope?.Pin.Value);
        Assert.Equal("SND-CONTEXT-102", typo.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pins_edited_after_the_map_was_built_leave_a_note_and_no_refusal()
    {
        var api = Repo(_billing);

        var pins = PinContext.Of([api], [api], Map(ruleset: "built-under-other-pins"), concept: null, []);

        Assert.Equal("pins changed after map v41 was built and the refresh for them didn't finish (fix: Refresh map on the Context page, or sindri context map refresh)", pins.Value.StaleNote);
    }

    [Fact, Trait("Lane", "unit")]
    public void Stale_map_note_says_building_while_the_refresh_job_runs()
    {
        var api = Repo(_billing);

        var pins = PinContext.Of([api], [api], Map(ruleset: "built-under-other-pins"), concept: null, []).Value.Rebuilding(new MapRebuildStatus(MapRebuildPhase.Building));

        Assert.Equal("pins changed; map v42 is being built with them, so some features are grouped the old way until it lands", pins.StaleNote);
    }
}
