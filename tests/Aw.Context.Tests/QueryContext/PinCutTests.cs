using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Features.QueryContext;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Graph;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Vectors;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.QueryContext;

/// <summary>Concept pins spec §3.2: under <c xml:lang="en">concept=</c> the graph part keeps the pin's nodes and the data stores they use.</summary>
public sealed class PinCutTests
{
    private const string Billing = "feature:api.services.billing";
    private const string Auth = "feature:api.services.auth";

    private static readonly ConceptPin _billing = ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: null, paths: ["services/billing/**"]);
    private static readonly ConceptPin _auth = ConceptPin.Of(ConceptId.From("auth"), "Auth", summary: null, features: null, paths: ["services/auth/**"]);

    private static GraphBuilder Pinned() => new GraphBuilder()
        .Edge(Billing, EdgeType.BelongsTo, "concept:billing")
        .Edge(Auth, EdgeType.BelongsTo, "concept:auth")
        .Edge("code:services/billing/charge.cs", EdgeType.Implements, Billing, provenance: Provenance.Deterministic)
        .Edge("code:services/auth/login.cs", EdgeType.Implements, Auth, provenance: Provenance.Deterministic)
        .Edge("surface:command/charge", EdgeType.PartOf, Billing)
        .Edge("action:billing.charge", EdgeType.On, "surface:command/charge")
        .Edge("action:billing.charge", EdgeType.Writes, "thing:invoices")
        .Edge("surface:screen/login", EdgeType.PartOf, Auth)
        .Edge("action:auth.audit", EdgeType.On, "surface:screen/login")
        .Edge("action:auth.audit", EdgeType.Reads, "thing:invoices");

    private static VisibleMap Map(GraphSnapshot graph)
    {
        var version = new MapVersion(41, ContextRepoName.From("api"), Commit: null, KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Current);
        return new VisibleMap(new MapSnapshot(version, graph, []), graph);
    }

    private static async Task<ContextResult> Ask(GraphSnapshot graph, string? concept)
    {
        var clock = new FakeTimeProvider();
        var recordStore = new FakeRecordStore();
        var pipeline = new QueryPipeline(new FakeScrubberFactory(), new SequentialIdGenerator(), clock, new FakeIntentStore(), new FakeModelPort(),
            new RecordSearch(new FakeLexicalLeg(), recordStore, new FakeQueryEmbedder(), new FakeVectorMatrixCache(recordStore), new FakeVectorScan()));
        var api = new ContextRepo(ContextRepoName.From("api"), "/repos/api") { Concepts = [_billing, _auth] };
        var scope = new ContextScope(Settings: null, [api], new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));
        var inputs = new PipelineInputs(new FixedMap(Map(graph)), new FixedSources([]), Concept: concept);

        return (await pipeline.Run(new QueryContextRequest(new ContextQuery(Text: null, Node: Billing), scope), inputs, TestContext.Current.CancellationToken)).Value;
    }

    private static IEnumerable<string?> Nodes(ContextResult result, string section) => result.Sections.Where(s => s.Name == section).SelectMany(s => s.Items).Select(i => i.Node);

    [Fact, Trait("Lane", "unit")]
    public async Task A_lens_under_a_pin_keeps_only_its_nodes_and_the_stores_they_write()
    {
        var unscoped = await Ask(Pinned().Build(), concept: null);
        var scoped = await Ask(Pinned().Build(), concept: "billing");

        Assert.Contains(Auth, Nodes(unscoped, "affects"), StringComparer.Ordinal);
        Assert.Contains("code:services/auth/login.cs", Nodes(unscoped, "code"), StringComparer.Ordinal);
        Assert.DoesNotContain(Auth, Nodes(scoped, "affects"), StringComparer.Ordinal);
        Assert.Equal(["code:services/billing/charge.cs"], Nodes(scoped, "code"), StringComparer.Ordinal);
        Assert.Contains(Billing, Nodes(scoped, "graph"), StringComparer.Ordinal);
    }
}
