using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Vectors;
using Aw.Context.Features.EmbedRecords;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Vectors;

namespace Aw.Context.Tests.EmbedRecords;

/// <summary>The embed job (owned-index spec §5.6, U10a): newest first, batches of the asked size, restartable, the guarded insert, and a model that goes down.</summary>
[Trait("Lane", "unit")]
public sealed class EmbedRecordsServiceTests
{
    private readonly FakeRecordStore _store = new();
    private readonly FakeChunkEmbedder _embedder = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private EmbedRecordsService Service() => new(_store, _embedder);

    private async Task Seed(params (string Key, int AgeDays, string[] Texts)[] records)
    {
        var writes = records.Select(r => RecordSamples.Write(RecordSamples.Record(r.Key) with { UpdatedAt = RecordSamples.Now.AddDays(-r.AgeDays) }, r.Texts)).ToArray();
        _ = (await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, writes), Ct)).Value;
    }

    private async Task<int> Stored() => (await _store.Vectors(VectorSpace.Chunk, 0, 10_000, Ct)).Value.Count;

    [Fact]
    public async Task Embeds_every_chunk_without_a_vector_and_reports_done()
    {
        await Seed(("linear:issue/1", 1, ["alpha beta", "gamma"]), ("linear:issue/2", 0, ["delta"]));

        var report = (await Service().Handle(new EmbedRecordsRequest(), Ct)).Value;

        Assert.Equal(new EmbedRecordsReport(3, 3, Done: true), report);
        Assert.Equal(3, await Stored());
        Assert.Empty((await _store.ChunksToEmbed(_embedder.Model, 10, Ct)).Value);
    }

    [Fact]
    public async Task Works_newest_record_first_in_batches_of_the_asked_size()
    {
        await Seed(("linear:issue/old", 9, ["old one", "old two"]), ("linear:issue/new", 0, ["new one"]));

        _ = await Service().Handle(new EmbedRecordsRequest(BatchSize: 2), Ct);

        Assert.Equal(["new one|old one", "old two"], _embedder.Batches.Select(b => string.Join('|', b)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Stops_at_the_limit_and_the_next_run_takes_up_where_it_left_off()
    {
        await Seed(("linear:issue/1", 2, ["a"]), ("linear:issue/2", 1, ["b"]), ("linear:issue/3", 0, ["c"]));

        var first = (await Service().Handle(new EmbedRecordsRequest(MaxChunks: 2), Ct)).Value;
        var second = (await Service().Handle(new EmbedRecordsRequest(MaxChunks: 2), Ct)).Value;

        Assert.Equal(new EmbedRecordsReport(2, 2, Done: false), first);
        Assert.Equal(new EmbedRecordsReport(1, 1, Done: true), second);
        Assert.Equal(["c", "b", "a"], _embedder.Batches.SelectMany(b => b), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_chunk_that_changed_while_its_batch_embedded_is_not_stored_and_is_embedded_again_with_its_new_text()
    {
        await Seed(("linear:issue/1", 0, ["old text"]));
        var rewritten = false;
        _embedder.OnEmbedded = () =>
        {
            if (rewritten)
            {
                return;
            }

            rewritten = true;
            var changed = RecordSamples.Record("linear:issue/1", "v2") with { UpdatedAt = RecordSamples.Now };
            _ = _store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2, RecordSamples.Write(changed, "new text")), Ct).AsTask().GetAwaiter().GetResult();
        };

        var report = (await Service().Handle(new EmbedRecordsRequest(), Ct)).Value;

        Assert.Equal(new EmbedRecordsReport(2, 1, Done: true), report);
        var stored = (await _store.Vectors(VectorSpace.Chunk, 0, 10, Ct)).Value.Single();
        Assert.Equal(RecordSamples.Hash("new text"), stored.Vector.TextHash);
    }

    [Fact]
    public async Task A_batch_that_stores_nothing_ends_the_run_instead_of_looping()
    {
        await Seed(("linear:issue/1", 0, ["old text"]));

        var report = (await new EmbedRecordsService(new VectorRefusingStore(_store), _embedder).Handle(new EmbedRecordsRequest(), Ct)).Value;

        Assert.Equal(new EmbedRecordsReport(1, 0, Done: false), report);
    }

    [Fact]
    public async Task A_model_that_is_down_fails_the_run_and_keeps_what_was_stored()
    {
        await Seed(("linear:issue/1", 1, ["a"]), ("linear:issue/2", 0, ["b"]));
        var calls = 0;
        _embedder.OnEmbedded = () =>
        {
            if (++calls == 1)
            {
                _embedder.Failure = ContextErrors.EmbedderUnavailable.With("model down");
            }
        };

        var result = await Service().Handle(new EmbedRecordsRequest(BatchSize: 1), Ct);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.EmbedderUnavailable.Code, result.Error.Code);
        Assert.Equal(1, await Stored());
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(100_001, 32)]
    [InlineData(10, 0)]
    [InlineData(10, 33)]
    public async Task A_limit_out_of_range_is_a_query_error(int max, int batch)
    {
        var result = await Service().Handle(new EmbedRecordsRequest(max, batch), Ct);

        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
    }
}
