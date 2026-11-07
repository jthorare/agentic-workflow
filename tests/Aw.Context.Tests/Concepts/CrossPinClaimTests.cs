using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Concepts;

/// <summary>Gh 1372, "features never span pins": a model tag from a file under one pin must not make that pin claim a feature whose deterministic home is another pin.</summary>
[Trait("Lane", "unit")]
public sealed class CrossPinClaimTests
{
    private const string Scheduling = "feature:app.src.scheduling";
    private const string Billing = "feature:app.src.billing";

    private static readonly ConceptPin[] _pins =
    [
        new(ConceptId.From("scheduling"), "Scheduling", Summary: null, [], ["src/scheduling/**"], [], []),
        new(ConceptId.From("billing"), "Billing", Summary: null, [], ["src/billing/**"], [], []),
    ];

    private static GraphEdge Edge(string from, EdgeType type, string to, Provenance provenance) => new(NodeKey.From(from), NodeKey.From(to), type, provenance);

    // The graph the build makes: each home feature belongs to its pin and is implemented by a file under it, deterministically.
    private static GraphSnapshot Graph(string[] extraFeatures, params GraphEdge[] extra) => new(
        [.. new[] { Scheduling, Billing }.Concat(extraFeatures).Select(k => new GraphNode(NodeKey.From(k), k))],
        [
            Edge(Scheduling, EdgeType.BelongsTo, "concept:scheduling", Provenance.Deterministic),
            Edge(Billing, EdgeType.BelongsTo, "concept:billing", Provenance.Deterministic),
            Edge("code:src/scheduling/shifts.cs", EdgeType.Implements, Scheduling, Provenance.Deterministic),
            Edge("code:src/billing/invoice.cs", EdgeType.Implements, Billing, Provenance.Deterministic),
            .. extra,
        ],
        [],
        []);

    [Fact]
    public void A_model_tag_from_a_file_under_one_pin_does_not_make_it_claim_a_feature_homed_in_another_pin()
    {
        var graph = Graph([], Edge("code:src/scheduling/shifts.cs", EdgeType.Implements, Billing, Provenance.Model));

        var (pinned, _) = ConceptPlan.Build(graph, _pins);

        var scheduling = pinned.Single(c => c.Slug == "scheduling").Features;
        var billing = pinned.Single(c => c.Slug == "billing").Features;

        Assert.DoesNotContain(Billing, scheduling, StringComparer.Ordinal);
        Assert.Contains(Billing, billing, StringComparer.Ordinal);
        Assert.Empty(scheduling.Intersect(billing, StringComparer.Ordinal));
    }

    [Fact]
    public void A_model_tagged_feature_with_no_home_in_any_pin_is_still_claimed_by_the_pin_its_file_is_under()
    {
        var graph = Graph(["feature:loose"], Edge("code:src/scheduling/shifts.cs", EdgeType.Implements, "feature:loose", Provenance.Model));

        var (pinned, _) = ConceptPlan.Build(graph, _pins);

        Assert.Contains("feature:loose", pinned.Single(c => c.Slug == "scheduling").Features, StringComparer.Ordinal);
    }
}
