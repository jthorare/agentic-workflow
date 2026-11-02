using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Vectors;

namespace Aw.Context.Tests.QueryContext;

/// <summary>Owned-index spec §4.4: a human caller is not scoped; a session keeps the profile's repo and Linear keys, so a Slack record is not its to read.</summary>
[Trait("Lane", "unit")]
public sealed class SlackAudienceQueryTests
{
    private const string Key = "slack:C0001GEN/1700000000.000100";
    private const string ChunkText = "orientation resets on restore";
    private static readonly AudienceKey _slack = AudienceKey.From("slack:C0001GEN");

    private const string Channels = """{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"}]}}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<FakeRecordStore> Stored(bool vector)
    {
        var store = new FakeRecordStore();
        var slack = SourceId.From("slack");
        var committed = await store.Commit(RecordSamples.Batch(slack, 1, RecordSamples.Write(RecordSamples.Record(Key, source: slack) with { Audience = _slack }, ChunkText)), Ct);
        Assert.False(committed.IsError);
        if (vector)
        {
            var query = (await new FakeQueryEmbedder().Embed("orientation", Ct)).Value.Vec.ToArray();
            _ = await store.PutVectors([new VectorWrite(VectorSpace.Chunk, new ChunkKey(RecordKey.From(Key), 0).ToString(), FakeQueryEmbedder.Model, RecordSamples.Hash(ChunkText), query)], Ct);
        }

        return store;
    }

    private static async Task<bool> Finds(ContextScope scope, bool dense)
    {
        var store = await Stored(dense);
        var lexical = dense ? null : new FakeLexicalLeg().Add(new ChunkKey(RecordKey.From(Key), 0), _slack);
        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical, store).Query(new ContextQuery("orientation"), scope, Ct);
        return result.Value.Cites.Values.Any(c => c.Key.Value == Key);
    }

    [Fact]
    public async Task A_human_finds_a_slack_record_through_the_lexical_leg() => Assert.True(await Finds(Engines.Human(Channels), dense: false));

    [Fact]
    public async Task A_human_finds_a_slack_record_through_the_dense_leg() => Assert.True(await Finds(Engines.Human(Channels), dense: true));

    [Fact]
    public async Task A_session_does_not_find_a_slack_record_through_the_lexical_leg() => Assert.False(await Finds(Engines.Session("split-panes", Channels), dense: false));

    [Fact]
    public async Task A_session_does_not_find_a_slack_record_through_the_dense_leg() => Assert.False(await Finds(Engines.Session("split-panes", Channels), dense: true));
}
