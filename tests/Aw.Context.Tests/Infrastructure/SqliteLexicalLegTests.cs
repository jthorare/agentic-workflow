using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Search;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class SqliteLexicalLegTests : IDisposable
{
    private static readonly AudienceKey _team = AudienceKey.From("linear:team-a");
    private static readonly AudienceKey _otherTeam = AudienceKey.From("linear:team-b");
    private static readonly AudienceKey _repo = AudienceKey.From("repo:acme/split-panes");

    private readonly TempHome _home = new();
    private readonly SqliteRecordStore _store;
    private readonly SqliteLexicalLeg _leg;

    public SqliteLexicalLegTests()
    {
        _store = new SqliteRecordStore(_home.Database);
        _leg = new SqliteLexicalLeg(_home.Database);
    }

    public void Dispose() => _home.Dispose();

    private static AudienceScope Scope(params AudienceKey[] keys) => new(keys.ToHashSet());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task Put(int page, params RecordWrite[] writes) => _ = await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, page, writes), Ct);

    private async Task<LexicalAnswer> Search(AudienceScope scope, params string[] terms) => (await _leg.Search(terms, scope, ILexicalLeg.DefaultLimit, Ct)).Value;

    [Fact]
    public async Task An_empty_or_missing_store_answers_no_hits_and_no_degraded_line_not_an_error()
    {
        var missing = await Search(Scope(_team), "orientation");
        Assert.Null(missing.Degraded);
        Assert.Empty(missing.Hits);

        _ = await _home.Database.Writable(Ct);
        var empty = await Search(Scope(_team), "orientation");
        Assert.Null(empty.Degraded);
        Assert.Empty(empty.Hits);
    }

    [Fact]
    public async Task A_term_finds_the_best_chunk_of_each_record_with_its_line_span()
    {
        var a = RecordSamples.Record("linear:issue/a");
        var b = RecordSamples.Record("linear:issue/b");
        await Put(1, RecordSamples.Write(a, "unrelated words", "orientation orientation resets on restore"), RecordSamples.Write(b, "orientation once"));
        var answer = await Search(Scope(_team), "orientation");

        Assert.Null(answer.Degraded);
        Assert.Equal(2, answer.Hits.Count);
        Assert.Equal(new ChunkKey(a.Key, 1), answer.Hits[0].Chunk);
        Assert.Equal(b.Key, answer.Hits[1].Chunk.Record);
        Assert.True(answer.Hits[0].Score >= answer.Hits[1].Score);
    }

    [Fact]
    public async Task A_listed_record_of_another_audience_is_found_and_an_unlisted_one_is_not()
    {
        var listed = RecordSamples.Record("linear:issue/listed") with { Audience = _otherTeam };
        var unlisted = RecordSamples.Record("linear:issue/unlisted") with { Audience = _otherTeam };
        await Put(1, RecordSamples.Write(listed, "orientation resets"), RecordSamples.Write(unlisted, "orientation resets"));
        var onlyRecords = new AudienceScope(new HashSet<AudienceKey>()) { Records = new HashSet<RecordKey> { listed.Key } };   // rule (c): no audience at all, one record listed

        Assert.Equal(listed.Key, Assert.Single((await Search(onlyRecords, "orientation")).Hits).Chunk.Record);
        Assert.Equal(2, (await Search(new AudienceScope(new HashSet<AudienceKey> { _otherTeam }), "orientation")).Hits.Count);
    }

    [Fact]
    public async Task Query_syntax_in_a_term_is_searched_as_words()
    {
        await Put(1, RecordSamples.Write(RecordSamples.Record("linear:issue/a"), "split pane orientation"));
        Assert.Empty((await Search(Scope(_team), "split AND (", "NEAR(", "\"", "orientation:x*")).Hits);
        _ = Assert.Single((await Search(Scope(_team), "split", "NOT (")).Hits);
        Assert.Empty((await Search(Scope(_team), "***")).Hits);
    }

    [Fact]
    public async Task The_mask_hides_other_audiences_records_with_none_and_removed_and_blocked_ones()
    {
        var mine = RecordSamples.Record("linear:issue/mine");
        var other = RecordSamples.Record("linear:issue/other") with { Audience = _otherTeam };
        var nobody = RecordSamples.Record("linear:issue/nobody") with { Audience = null };
        var gone = RecordSamples.Record("linear:issue/gone");
        var blocked = RecordSamples.Record("linear:issue/blocked", blocked: true);
        await Put(1, RecordSamples.Write(mine, "shared words"), RecordSamples.Write(other, "shared words"), RecordSamples.Write(nobody, "shared words"),
            RecordSamples.Write(gone, "shared words"), RecordSamples.Write(blocked));
        _ = await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [gone.Key] }, Ct);

        var hits = (await Search(Scope(_team), "shared")).Hits;
        Assert.Equal([mine.Key], hits.Select(h => h.Chunk.Record));
        Assert.Equal([other.Key], (await Search(Scope(_otherTeam), "shared")).Hits.Select(h => h.Chunk.Record));
        Assert.Equal(2, (await Search(Scope(_team, _otherTeam), "shared")).Hits.Count);
    }

    [Fact]
    public async Task The_mask_is_applied_before_the_limit_so_a_small_scope_still_gets_its_hits()
    {
        var others = Enumerable.Range(0, 5).Select(i => RecordSamples.Write(RecordSamples.Record("linear:issue/o" + new string('x', i + 1)) with { Audience = _otherTeam }, "needle needle needle")).ToArray();
        await Put(1, [.. others, RecordSamples.Write(RecordSamples.Record("linear:issue/mine"), "needle")]);
        var answer = (await _leg.Search(["needle"], Scope(_team), limit: 2, Ct)).Value;
        _ = Assert.Single(answer.Hits);
    }

    [Fact]
    public async Task An_empty_scope_or_a_scope_that_allows_nothing_answers_like_no_match_not_degraded()
    {
        await Put(1, RecordSamples.Write(RecordSamples.Record("linear:issue/a"), "needle"));
        var none = await Search(AudienceScope.None, "needle");
        Assert.Null(none.Degraded);
        Assert.Empty(none.Hits);
        Assert.Empty((await Search(Scope(_repo), "needle")).Hits);
        Assert.Empty((await Search(Scope(_team), "absent")).Hits);
    }
}
