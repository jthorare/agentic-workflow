using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Ranker;

/// <summary>Concept pins spec §3.3: a leg of the anchor's pin, after RRF, lifts the docs inside it.</summary>
public sealed class PinLegTests
{
    private static SourceHit Hit(string source, string key, int rank) => new(
        new SourceRef(SourceId.From(source), ExternalId.From(key), SourceVersion.From("v1"), Permalink: null, RecordKey.From(key)),
        "t", rank, DateTimeOffset.UnixEpoch, Leaky: false);

    private const string Notify = "repo:api/notify/docs/refund-emails.md";
    private const string Billing = "repo:api/billing/docs/refunds.md";
    private const string Eng482 = "linear:ENG-482";

    private static PinScope BillingPin()
    {
        var api = new ContextRepo(ContextRepoName.From("api"), "/api") { Concepts = [ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: null, paths: ["billing/**"])] };
        var graph = new GraphBuilder().Node("concept:billing").Edge("record:" + Eng482, EdgeType.Mentions, "concept:billing").Build();
        return PinScope.Of([api], [api], "billing", graph, "v1").Value;
    }

    // The worked example of §3.3: the docs leg and the lexical leg both put the notify doc first.
    private static readonly SourceAnswer[] _answers =
    [
        new(SourceId.From("repo-docs"), [Hit("repo-docs", Notify, 1), Hit("repo-docs", Billing, 2)]),
        new(SourceId.From("lexical"), [Hit("lexical", Notify, 1), Hit("lexical", Eng482, 2), Hit("lexical", Billing, 3)]),
    ];

    [Fact, Trait("Lane", "unit")]
    public void Pin_leg_lifts_the_billing_doc_above_the_notify_doc()
    {
        var plain = RankFusion.Fuse(_answers);
        Assert.Equal([Notify, Billing, Eng482], plain.Select(f => f.Key.Value), StringComparer.Ordinal);

        var leg = PinLeg.Of(plain, BillingPin());
        var fused = RankFusion.Fuse(_answers, [leg]);

        Assert.Equal([Billing, Notify, Eng482], fused.Select(f => f.Key.Value), StringComparer.Ordinal);   // in-pin order is fused order: billing doc #1, ENG-482 #2
        Assert.Equal((1d / 62) + (1d / 63) + (1d / 61), fused[0].Score, 12);
        Assert.Equal((1d / 62) + (1d / 62), fused[2].Score, 12);
        Assert.Equal(["rrf:repo-docs#2", "rrf:lexical#3", "rrf:pin#1"], fused[0].Why);
        Assert.Equal((1d / 61) + (1d / 61), fused[1].Score, 12);   // outside the pin: unchanged
        Assert.Equal(["rrf:repo-docs#1", "rrf:lexical#1"], fused[1].Why);
    }
}
