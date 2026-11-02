using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.QueryContext;

[Trait("Lane", "unit")]
public sealed class LexicalLegQueryTests
{
    private const string Teams = """{"sources":{"linear":{"teams":["SPL"]}}}""";
    private const string DocKey = "repo:split-panes/docs/decisions/0001-orientation-toggle.md";
    private static readonly AudienceKey _allowed = AudienceKey.From("linear:SPL");
    private static readonly AudienceKey _denied = AudienceKey.From("linear:OTHER");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<FakeRecordStore> Stored(params (string Key, AudienceKey Audience)[] rows)
    {
        var store = new FakeRecordStore();
        foreach (var (key, audience) in rows)
        {
            _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record(key) with { Audience = audience }, "orientation resets on restore")), Ct);
        }

        return store;
    }

    private static ChunkKey Chunk(string key) => new(RecordKey.From(key), 0);

    [Fact]
    public async Task A_lexical_hit_is_in_the_answer_and_fuses_with_another_sources_hit_by_rank()
    {
        var store = await Stored(("linear:issue/a", _allowed), ("linear:issue/b", _allowed));
        var leg = new FakeLexicalLeg().Add(Chunk("linear:issue/a"), _allowed).Add(Chunk("linear:issue/b"), _allowed);

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: leg, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        var items = result.Value.Sections.Single().Items;
        var a = Assert.Single(items, i => result.Value.Cites[i.Cites.Single()].Key.Value == "linear:issue/a");
        var b = Assert.Single(items, i => result.Value.Cites[i.Cites.Single()].Key.Value == "linear:issue/b");
        Assert.Equal(["rrf:records#1"], a.Why, StringComparer.Ordinal);
        Assert.Equal(["rrf:records#2"], b.Why, StringComparer.Ordinal);
        Assert.Equal(["orientation"], leg.LastTerms, StringComparer.Ordinal);
        Assert.Empty(result.Value.Degraded);
    }

    [Fact]
    public async Task A_record_both_a_source_and_the_store_found_is_one_item_with_both_ranks()
    {
        var store = await Stored((DocKey, _allowed));
        var leg = new FakeLexicalLeg().Add(Chunk(DocKey), _allowed);

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: leg, records: store).Query(new ContextQuery("orientation toggle"), Engines.Human(Teams), Ct);

        var top = result.Value.Sections.Single().Items[0];
        Assert.Equal(DocKey, result.Value.Cites[top.Cites.Single()].Key.Value);
        Assert.Contains("rrf:records#1", top.Why, StringComparer.Ordinal);
        Assert.Contains(top.Why, w => w.StartsWith("rrf:repo-docs#", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_record_the_callers_audiences_do_not_allow_never_appears()
    {
        var store = await Stored(("linear:issue/seen", _allowed), ("linear:issue/hidden", _denied));
        var leg = new FakeLexicalLeg().Add(Chunk("linear:issue/seen"), _allowed).Add(Chunk("linear:issue/hidden"), _denied);

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: leg, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        var keys = result.Value.Cites.Values.Select(c => c.Key.Value).ToList();
        Assert.Contains("linear:issue/seen", keys, StringComparer.Ordinal);
        Assert.DoesNotContain("linear:issue/hidden", keys, StringComparer.Ordinal);
        Assert.Equal([_allowed], leg.LastScope!.Allowed.Where(k => k.Value.StartsWith("linear:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_store_that_cannot_be_read_is_a_degraded_source_and_the_others_still_answer()
    {
        var leg = new FakeLexicalLeg { Failure = ContextErrors.StoreUnavailable.With("the store is locked") };

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: leg).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        var degraded = Assert.Single(result.Value.Degraded);
        Assert.Equal("records", degraded.Source.Value);
        Assert.Contains("the store is locked", degraded.Message, StringComparison.Ordinal);
        Assert.NotEmpty(result.Value.Sections.Single().Items);
    }

    [Fact]
    public async Task A_degraded_leg_is_told_with_its_own_wording_and_drops_out()
    {
        var leg = new FakeLexicalLeg { Degraded = new LexicalDegraded(ContextErrors.SourceUnavailable.Code, "the record store is empty or not synced yet; fix: sindri index sync") };

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), lexical: leg).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        var degraded = Assert.Single(result.Value.Degraded);
        Assert.Equal(("records", ContextErrors.SourceUnavailable.Code, "the record store is empty or not synced yet; fix: sindri index sync"), (degraded.Source.Value, degraded.Code, degraded.Message));
        Assert.NotEmpty(result.Value.Sections.Single().Items);
    }

    [Fact]
    public async Task The_store_answering_nothing_does_not_hide_that_every_catalog_source_is_down()
    {
        var docs = FakeContextSource.FixtureDocs();
        docs.Failure = ContextErrors.SourceUnavailable.With("down");

        var result = await Engines.Over(docs).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        Assert.Equal(ContextErrors.AllSourcesUnavailable.Code, result.Error.Code);
    }
}
