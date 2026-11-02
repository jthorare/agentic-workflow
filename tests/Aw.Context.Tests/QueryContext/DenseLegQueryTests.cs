using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Vectors;
using Aw.Context.Features.QueryContext;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Vectors;

namespace Aw.Context.Tests.QueryContext;

[Trait("Lane", "unit")]
public sealed class DenseLegQueryTests
{
    private const string Teams = """{"sources":{"linear":{"teams":["SPL"]}}}""";
    private const string ChunkText = "orientation resets on restore";
    private static readonly AudienceKey _allowed = AudienceKey.From("linear:SPL");
    private static readonly AudienceKey _denied = AudienceKey.From("linear:OTHER");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<float[]> QueryVector()
    {
        var embedded = await new FakeQueryEmbedder().Embed("orientation", Ct);
        return embedded.Value.Vec.ToArray();
    }

    // The direction of the query itself (distance 0) or a basis vector the query has no part in (distance 1).
    private static async Task<float[]> Direction(bool near)
    {
        var query = await QueryVector();
        if (near)
        {
            return query;
        }

        var free = Array.FindIndex(query, c => c == 0f);
        return [.. Enumerable.Range(0, query.Length).Select(i => i == free ? 1f : 0f)];
    }

    private static async Task<FakeRecordStore> Stored(params (string Key, AudienceKey Audience, bool Near, bool Vector)[] rows)
    {
        var store = new FakeRecordStore();
        foreach (var (key, audience, near, vector) in rows)
        {
            var chunk = new ChunkKey(RecordKey.From(key), 0);
            _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record(key) with { Audience = audience }, ChunkText)), Ct);
            if (vector)
            {
                _ = await store.PutVectors([new VectorWrite(VectorSpace.Chunk, chunk.ToString(), FakeQueryEmbedder.Model, RecordSamples.Hash(ChunkText), await Direction(near))], Ct);
            }
        }

        return store;
    }

    private static ChunkKey Chunk(string key) => new(RecordKey.From(key), 0);

    private static IReadOnlyList<string> Keys(Context.Core.Results.ContextResult result) => [.. result.Cites.Values.Select(c => c.Key.Value)];

    [Fact]
    public async Task A_record_only_the_dense_leg_found_is_in_the_answer()
    {
        var store = await Stored(("linear:issue/near", _allowed, true, true));

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: null, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        var item = Assert.Single(result.Value.Sections.Single().Items, i => result.Value.Cites[i.Cites.Single()].Key.Value == "linear:issue/near");
        Assert.Equal(["rrf:records-dense#1"], item.Why, StringComparer.Ordinal);
        Assert.Empty(result.Value.Degraded);
    }

    [Fact]
    public async Task A_record_both_legs_found_is_one_item_with_both_ranks()
    {
        var store = await Stored(("linear:issue/both", _allowed, true, true));
        var leg = new FakeLexicalLeg().Add(Chunk("linear:issue/both"), _allowed);

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: leg, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        var item = Assert.Single(result.Value.Sections.Single().Items, i => result.Value.Cites[i.Cites.Single()].Key.Value == "linear:issue/both");
        Assert.Contains("rrf:records#1", item.Why, StringComparer.Ordinal);
        Assert.Contains("rrf:records-dense#1", item.Why, StringComparer.Ordinal);
        _ = Assert.Single(Keys(result.Value), k => k == "linear:issue/both");
    }

    [Fact]
    public async Task A_vector_of_a_record_the_callers_audiences_do_not_allow_never_appears()
    {
        var store = await Stored(("linear:issue/seen", _allowed, true, true), ("linear:issue/hidden", _denied, true, true));

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: null, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        Assert.Contains("linear:issue/seen", Keys(result.Value), StringComparer.Ordinal);
        Assert.DoesNotContain("linear:issue/hidden", Keys(result.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_vector_far_from_the_query_is_cut_by_the_floor()
    {
        var store = await Stored(("linear:issue/near", _allowed, true, true), ("linear:issue/far", _allowed, false, true));

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: null, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        Assert.Contains("linear:issue/near", Keys(result.Value), StringComparer.Ordinal);
        Assert.DoesNotContain("linear:issue/far", Keys(result.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_model_that_is_down_drops_the_dense_leg_with_a_plain_line_and_the_others_still_answer()
    {
        var store = await Stored(("linear:issue/near", _allowed, true, true));
        var embedder = new FakeQueryEmbedder { Failure = ContextErrors.EmbedderUnavailable.With("connection refused") };
        var leg = new FakeLexicalLeg().Add(Chunk("linear:issue/near"), _allowed);

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: leg, records: store, embedder: embedder).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        var degraded = Assert.Single(result.Value.Degraded);
        Assert.Equal("records-dense", degraded.Source.Value);
        Assert.Equal("meaning search is off: Ollama isn't running", degraded.Message);
        Assert.Contains("linear:issue/near", Keys(result.Value), StringComparer.Ordinal);
        Assert.NotEmpty(result.Value.Sections.Single().Items);
    }

    [Fact]
    public async Task No_vectors_yet_is_no_dense_hits_and_no_degraded_line()
    {
        var store = await Stored(("linear:issue/plain", _allowed, true, false));
        var leg = new FakeLexicalLeg().Add(Chunk("linear:issue/plain"), _allowed);

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: leg, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        Assert.Empty(result.Value.Degraded);
        var item = Assert.Single(result.Value.Sections.Single().Items, i => result.Value.Cites[i.Cites.Single()].Key.Value == "linear:issue/plain");
        Assert.DoesNotContain(item.Why, w => w.Contains("records-dense", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Vectors_for_some_of_the_records_say_how_much_meaning_search_covers()
    {
        var store = await Stored(("linear:issue/a", _allowed, true, true), ("linear:issue/b", _allowed, true, false), ("linear:issue/c", _allowed, true, false), ("linear:issue/d", _allowed, true, false));

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: null, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        var degraded = Assert.Single(result.Value.Degraded);
        Assert.Equal("meaning search covers 25% of your records; the rest are word-search only", degraded.Message);
    }

    [Fact]
    public void The_floor_keeps_everything_within_half_a_distance_else_the_nearest_eight_within_three_quarters_else_nothing()
    {
        static VectorHit Hit(int row, float distance) => new(row, "r" + row.ToString(System.Globalization.CultureInfo.InvariantCulture) + "#0", 1f - distance);

        var near = DenseFloor.Apply([Hit(0, 0.1f), Hit(1, 0.5f), Hit(2, 0.6f)]);
        Assert.Equal([0, 1], near.Select(h => h.Row));

        var rescued = DenseFloor.Apply([.. Enumerable.Range(0, 12).Select(i => Hit(i, 0.6f + (i * 0.01f)))]);
        Assert.Equal(Enumerable.Range(0, 8), rescued.Select(h => h.Row));

        Assert.Empty(DenseFloor.Apply([Hit(0, 0.8f), Hit(1, 0.9f)]));
    }
}
