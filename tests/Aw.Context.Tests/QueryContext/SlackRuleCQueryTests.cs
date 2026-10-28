using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Vectors;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.QueryContext;

/// <summary>Owned-index spec §4.4, rule (c): a session reaches a Slack thread only through a 'mentions' link from the thread to one of its repo's graph nodes.</summary>
[Trait("Lane", "unit")]
public sealed class SlackRuleCQueryTests : IDisposable
{
    private const string Linked = "slack:C0001GEN/1700000000.000100";
    private const string Elsewhere = "slack:C0001GEN/1700000000.000200";
    private const string ChunkText = "zebracrossing handover notes";
    private const string Term = "zebracrossing";
    private static readonly AudienceKey _slack = AudienceKey.From("slack:C0001GEN");

    private const string Settings = """{"sessions":{"enabled":true},"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"}]}}}""";

    private readonly List<IDisposable> _worlds = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (var world in _worlds)
        {
            world.Dispose();
        }
    }

    private static ContextScope Session()
    {
        using var section = System.Text.Json.JsonDocument.Parse(Settings);
        return new(section.RootElement.Clone(), MapWorld.Repos, new ContextCaller(ContextCallerKind.Session, ContextRepoName.From(FixtureMap.Repo), ContextClient.Http));
    }

    private async Task<HashSet<string>> Found(bool dense, params string[] keys)
    {
        var store = new FakeRecordStore();
        var slack = SourceId.From("slack");
        var vec = (await new FakeQueryEmbedder().Embed(Term, Ct)).Value.Vec.ToArray();
        var lexical = new FakeLexicalLeg();
        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            var committed = await store.Commit(RecordSamples.Batch(slack, i + 1, RecordSamples.Write(RecordSamples.Record(key, source: slack) with { Audience = _slack }, ChunkText)), Ct);
            Assert.False(committed.IsError);
            if (dense)
            {
                _ = await store.PutVectors([new VectorWrite(VectorSpace.Chunk, new ChunkKey(RecordKey.From(key), 0).ToString(), FakeQueryEmbedder.Model, RecordSamples.Hash(ChunkText), vec)], Ct);
            }
            else
            {
                _ = lexical.Add(new ChunkKey(RecordKey.From(key), 0), _slack);
            }
        }

        var world = new MapWorld(records: store, lexical: lexical);
        _worlds.Add(world);
        _ = await world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:in\", label: In}", "  - {key: \"feature:out\", label: Out}",
            "  - {key: \"code:src/in.ts\", label: in.ts, provenance: deterministic}", "  - {key: \"code:src/out.ts\", label: out.ts, provenance: deterministic}",
            $"  - {{key: \"record:{Linked}\", label: Linked thread}}", $"  - {{key: \"record:{Elsewhere}\", label: Other thread}}",
            "edges:",
            "  - {from: \"code:src/in.ts\", to: \"feature:in\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/in.ts\"}]}",
            "  - {from: \"code:src/out.ts\", to: \"feature:out\", type: implements, provenance: deterministic, evidence: [{key: \"repo:other/src/out.ts\"}]}",
            $"  - {{from: \"record:{Linked}\", to: \"feature:in\", type: mentions}}",
            $"  - {{from: \"record:{Elsewhere}\", to: \"feature:out\", type: mentions}}"));

        var result = await world.Engine.Query(new ContextQuery(Term), Session(), Ct);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return result.Value.Cites.Values.Select(c => c.Key.Value).ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_session_finds_a_Slack_thread_linked_to_its_repo() => Assert.Contains(Linked, await Found(dense: false, Linked, Elsewhere));

    [Fact]
    public async Task A_session_finds_a_linked_Slack_thread_through_the_dense_leg() => Assert.Contains(Linked, await Found(dense: true, Linked, Elsewhere));

    [Fact]
    public async Task A_session_does_not_find_a_Slack_thread_linked_only_to_another_repo()
    {
        Assert.DoesNotContain(Elsewhere, await Found(dense: false, Linked, Elsewhere));
        Assert.DoesNotContain(Elsewhere, await Found(dense: true, Linked, Elsewhere));
    }
}
