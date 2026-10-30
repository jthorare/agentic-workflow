using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;
using Aw.Kernel;

namespace Aw.Context.Tests.Concepts;

/// <summary>Concept pins spec §3.1: one resolver says what is inside a pin, from the map's own grouping.</summary>
public sealed class PinScopeTests
{
    private static ConceptPin Pin(string id, string name, string[]? paths = null, string[]? features = null) =>
        ConceptPin.Of(ConceptId.From(id), name, summary: null, features, paths);

    private static ContextRepo Repo(string name, params ConceptPin[] pins) => new(ContextRepoName.From(name), "/" + name) { Concepts = pins };

    // api: billing holds services/billing, auth holds services/auth; every file implements its directory's home feature.
    private static GraphSnapshot Graph(params string[] concepts) => Plus(concepts, new GraphBuilder()
        .Edge("code:services/billing/charge.cs", EdgeType.Implements, "feature:api.services.billing", provenance: Provenance.Deterministic)
        .Edge("code:services/auth/login.cs", EdgeType.Implements, "feature:api.services.auth", provenance: Provenance.Deterministic)
        .Edge("code:services/auth/login.cs", EdgeType.Implements, "feature:tag.login", provenance: Provenance.Deterministic)
        .Edge("feature:api.services.billing", EdgeType.BelongsTo, "concept:billing")
        .Edge("feature:api.services.auth", EdgeType.BelongsTo, "concept:auth")
        .Edge("feature:tag.login", EdgeType.BelongsTo, "concept:billing")
        .Edge("record:linear/ENG-1", EdgeType.Mentions, "concept:billing")
        .Edge("record:linear/ENG-2", EdgeType.Mentions, "feature:api.services.auth"));

    // The map has a concept node for every pin it was built with.
    private static GraphSnapshot Plus(string[] concepts, GraphBuilder builder)
    {
        foreach (var concept in concepts)
        {
            _ = builder.Node("concept:" + concept);
        }

        return builder.Build();
    }

    private static readonly ConceptPin _billing = Pin("billing", "Billing", paths: ["services/billing/**"]);
    private static readonly string[] _eight = ["p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8"];
    private static readonly ConceptPin _auth = Pin("auth", "Auth", paths: ["services/auth/**"]);

    [Fact, Trait("Lane", "unit")]
    public void Pin_of_a_file_is_the_pin_of_its_home_feature()
    {
        var api = Repo("api", _billing, _auth);

        var set = PinSet.Of([api], [api], Graph());

        Assert.Equal("billing", set.PinOf(NodeKey.From("code:services/billing/charge.cs"))?.Value);
        Assert.Equal("auth", set.PinOf(NodeKey.From("code:services/auth/login.cs"))?.Value);   // not billing, which a tag feature belongs to
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_holds_the_nodes_of_its_features_its_concept_and_the_records_that_mention_it()
    {
        var api = Repo("api", _billing, _auth);

        var billing = PinScope.Of([api], [api], "billing", Graph(), "v41").Value;

        Assert.True(billing.HoldsNode(NodeKey.From("concept:billing")));
        Assert.True(billing.HoldsNode(NodeKey.From("feature:api.services.billing")));
        Assert.True(billing.HoldsNode(NodeKey.From("record:linear/ENG-1")));
        Assert.False(billing.HoldsNode(NodeKey.From("record:linear/ENG-2")));   // mentions a feature of auth
        Assert.False(billing.HoldsNode(NodeKey.From("code:services/auth/login.cs")));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_id_may_be_written_with_its_concept_prefix()
    {
        var api = Repo("api", _billing);

        Assert.Equal("billing", PinScope.Of([api], [api], "concept:billing", Graph(), "v41").Value.Pin.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_pin_of_a_repo_that_matches_a_path_holds_it()
    {
        var wide = Pin("wide", "Wide", paths: ["services/**"]);
        var api = Repo("api", wide, _billing);

        var billing = PinScope.Of([api], [api], "billing", Graph(), "v41").Value;
        var holdsWide = PinScope.Of([api], [api], "wide", Graph("wide"), "v41").Value;

        Assert.False(billing.HoldsPath(api.Name, "services/billing/charge.cs"));
        Assert.True(holdsWide.HoldsPath(api.Name, "services/billing/charge.cs"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_path_only_an_earlier_pin_of_another_repo_matches_is_still_this_pins_in_this_repo()
    {
        var web = Repo("web", Pin("wide", "Wide", paths: ["services/**"]));
        var api = Repo("api", _billing);

        var billing = PinScope.Of([web, api], [web, api], "billing", Graph(), "v41").Value;

        Assert.True(billing.HoldsPath(api.Name, "services/billing/charge.cs"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_features_only_pin_holds_its_files_and_a_leaf_claimed_by_two_pins_goes_to_the_first()
    {
        var byFeature = Pin("pay", "Pay", features: ["feature:api.services.billing"]);
        var api = Repo("api", byFeature, _billing);
        var graph = new GraphBuilder()
            .Edge("code:services/billing/charge.cs", EdgeType.Implements, "feature:api.services.billing", provenance: Provenance.Deterministic)
            .Edge("feature:api.services.billing", EdgeType.BelongsTo, "concept:billing")
            .Edge("feature:api.services.billing", EdgeType.BelongsTo, "concept:pay")
            .Build();

        var set = PinSet.Of([api], [api], graph);

        Assert.Equal("pay", set.PinOf(NodeKey.From("code:services/billing/charge.cs"))?.Value);
        Assert.Equal("pay", set.PinOf(NodeKey.From("feature:api.services.billing"))?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ids_are_made_over_the_mapped_repos_so_a_smaller_view_never_renames_a_pin()
    {
        var web = Repo("web", Pin("billing", "Billing", paths: ["web/**"]));
        var api = Repo("api", Pin("billing", "Billing", paths: ["services/**"]));   // a different pin: over both repos it is billing-2

        var theirs = PinScope.Of([web, api], [api], "billing-2", Graph("billing-2"), "v41");
        var unseen = PinScope.Of([web, api], [api], "billing", Graph("billing"), "v41");

        Assert.Equal("billing-2", theirs.Value.Pin.Value);
        Assert.Equal(ContextErrors.PinUnknown.Code, unseen.Error.Code);   // 'billing' is web's pin, which this caller can't see
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_only_an_unseen_repo_has_answers_like_a_typo_and_names_only_visible_pins()
    {
        var web = Repo("web", Pin("secret", "Secret", paths: ["web/**"]));
        var api = Repo("api", _billing, _auth);

        var typo = PinScope.Of([web, api], [api], "bilin", Graph(), "v41");
        var unseen = PinScope.Of([web, api], [api], "secret", Graph(), "v41");

        Assert.Equal(ContextErrors.PinUnknown.Code, typo.Error.Code);
        Assert.Equal("no pin is named 'bilin' (fix: try billing, auth)", typo.Error.Message);
        Assert.Equal("no pin is named 'secret' (fix: try auth, billing)", unseen.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void Suggestions_are_at_most_five()
    {
        var pins = _eight.Select(id => Pin(id, id, paths: [id + "/**"])).ToArray();
        var api = Repo("api", pins);

        var error = PinScope.Of([api], [api], "zzz", Graph(), "v41").Error;

        Assert.Equal(5, error.Message[(error.Message.IndexOf("try ", StringComparison.Ordinal) + 4)..^1].Split(", ").Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_pins_at_all_is_103()
    {
        var api = Repo("api");

        var error = PinScope.Of([api], [api], "billing", Graph(), "v41").Error;

        Assert.Equal(ContextErrors.NoPins.Code, error.Code);
        Assert.Equal("no pins are set up (fix: add index.concepts to the profile)", error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_the_map_has_no_concept_for_is_newer_than_the_map()
    {
        var api = Repo("api", _billing, Pin("fresh", "Fresh", paths: ["fresh/**"]));

        var error = PinScope.Of([api], [api], "fresh", Graph(), "v41").Error;

        Assert.Equal(ContextErrors.PinNewerThanMap.Code, error.Code);
        Assert.Equal("pin 'fresh' is newer than map v41 (fix: sindri context map build)", error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_four_query_side_errors_exit_as_attention()
    {
        ErrorDefinition[] errors = [ContextErrors.PinUnknown, ContextErrors.NoPins, ContextErrors.PinNewerThanMap, ContextErrors.PinNeedsMap];

        Assert.All(errors, e => Assert.Contains(e.Code, ContextErrors.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_anchors_pin_is_found_without_a_scope()
    {
        var api = Repo("api", _billing, _auth);

        Assert.Equal("auth", PinScope.PinOf(NodeKey.From("concept:auth"), [api], Graph())?.Value);
        Assert.Null(PinScope.PinOf(NodeKey.From("feature:nowhere"), [api], Graph()));
    }
}
