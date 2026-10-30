using System.Text.Json;
using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.QueryContext;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Graph;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Vectors;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.QueryContext;

/// <summary>Concept pins spec §3.2 (Q2b): the record legs and the sources take the pin, so each leg's limit fills with in-pin results, and an empty answer says where the results read were.</summary>
[Trait("Lane", "unit")]
public sealed class PinnedRecordLegsTests
{
    private const string Teams = """{"sources":{"linear":{"teams":["SPL"]}}}""";
    private static readonly AudienceKey _audience = AudienceKey.From("linear:SPL");
    private static readonly ConceptPin _billing = ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: null, paths: ["services/billing/**"]);
    private static readonly ConceptPin _auth = ConceptPin.Of(ConceptId.From("auth"), "Auth", summary: null, features: null, paths: ["services/auth/**"]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ContextRepo Api => new(ContextRepoName.From("api"), "/api") { Concepts = [_billing, _auth] };

    private static ChunkKey Chunk(string key) => new(RecordKey.From(key), 0);

    private static async Task<FakeRecordStore> Stored(IEnumerable<string> keys)
    {
        var store = new FakeRecordStore();
        foreach (var key in keys)
        {
            _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record(key) with { Audience = _audience }, "orientation resets on restore")), Ct);
        }

        return store;
    }

    // Each record node gets a mention edge to its pin's concept; a key in neither list has no node, so no pin.
    private static VisibleMap MapOf(IEnumerable<string> billing, IEnumerable<string> auth)
    {
        var graph = new GraphBuilder();
        _ = graph.Node("concept:billing", "Billing").Node("concept:auth", "Auth");
        foreach (var key in billing)
        {
            _ = graph.Edge("record:" + key, EdgeType.Mentions, "concept:billing");
        }

        foreach (var key in auth)
        {
            _ = graph.Edge("record:" + key, EdgeType.Mentions, "concept:auth");
        }

        var built = graph.Build();
        var version = new MapVersion(41, ContextRepoName.From("api"), Commit: null, KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Current) { Ruleset = MapRuleset.Of([Api]) };
        return new VisibleMap(new MapSnapshot(version, built, []), built);
    }

    private static async Task<Result<ContextResult>> Ask(FakeLexicalLeg leg, FakeRecordStore store, VisibleMap map, string? concept, params IContextSource[] sources)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        var pipeline = new QueryPipeline(new FakeScrubberFactory(), new SequentialIdGenerator(), clock, new FakeIntentStore(), new FakeModelPort(),
            new RecordSearch(leg, store, new FakeQueryEmbedder(), new FakeVectorMatrixCache(store), new FakeVectorScan()));
        var scope = new ContextScope(JsonDocument.Parse(Teams).RootElement.Clone(), [Api], new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));
        return await pipeline.Run(new QueryContextRequest(new ContextQuery("orientation"), scope), new PipelineInputs(new FixedMap(map), new FixedSources(sources), Concept: concept), Ct);
    }

    [Fact]
    public async Task Record_legs_under_a_pin_fill_their_limit_with_in_pin_records()
    {
        string[] outside = [.. Enumerable.Range(0, 60).Select(i => string.Create(CultureInfo.InvariantCulture, $"linear:issue/out{i}"))];
        string[] inside = ["linear:issue/in0", "linear:issue/in1", "linear:issue/in2"];
        var store = await Stored([.. outside, .. inside]);
        var leg = new FakeLexicalLeg();
        foreach (var key in outside.Concat(inside))
        {
            _ = leg.Add(Chunk(key), _audience);
        }

        var result = await Ask(leg, store, MapOf(inside, auth: []), "billing");

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(inside.Order(StringComparer.Ordinal), result.Value.Cites.Values.Select(c => c.Key.Value).Where(k => k.StartsWith("linear:", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }
}
