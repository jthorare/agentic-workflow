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

    // The map a caller reads is the stored graph with the owner's overrides and the synced records laid over it; the pins hold what that map shows.
    private static VisibleMap Overlaid(GraphSnapshot visible)
    {
        var stored = Map(MapRuleset.Of([Repo(_billing)]));
        return new VisibleMap(stored.Snapshot, visible);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_record_linked_in_the_overlaid_map_sits_in_the_pin()
    {
        var api = Repo(_billing);
        var visible = new GraphBuilder()
            .Edge("feature:api.services.billing", EdgeType.BelongsTo, "concept:billing")
            .Edge("record:refunds-ticket", EdgeType.Mentions, "feature:api.services.billing")
            .Build();

        var pins = PinContext.Of([api], [api], Overlaid(visible), concept: null, ["record:refunds-ticket"]);

        Assert.Equal("billing", Assert.Single(pins.Value.AnchorPins).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_naming_a_feature_the_owner_renamed_still_holds_its_files()
    {
        var api = Repo(_billing);
        var visible = new GraphBuilder()
            .Node("feature:api.services.billing", label: "Payments")
            .Edge("code:services/billing/charge.cs", EdgeType.Implements, "feature:api.services.billing", provenance: Provenance.Deterministic)
            .Edge("feature:api.services.billing", EdgeType.BelongsTo, "concept:billing")
            .Build();

        var pins = PinContext.Of([api], [api], Overlaid(visible), concept: "billing", []);

        Assert.True(pins.Value.Scope?.HoldsNode(NodeKey.From("code:services/billing/charge.cs")));
    }
}
