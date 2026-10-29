using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Vectors;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Vectors;

/// <summary>The append-only vector matrix (owned-index spec §4.4, U10a): it loads only what is new, and starts over on a delete or another model.</summary>
[Trait("Lane", "unit")]
public sealed class VectorMatrixCacheTests : IDisposable
{
    private const string Model = "fake-model@1";

    private readonly CountingStore _store = new();
    private readonly VectorMatrixCache _cache;

    public VectorMatrixCacheTests()
    {
        _cache = new VectorMatrixCache(_store);
    }

    public void Dispose() => _cache.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task Put(string record, string version, params string[] texts)
    {
        var write = RecordSamples.Write(RecordSamples.Record(record, version), texts);
        _ = (await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, write), Ct)).Value;
        var vectors = texts.Select((text, seq) => RecordSamples.Vector(new ChunkKey(write.Record.Key, seq), text, seq + 1, 1)).ToList();
        _ = (await _store.PutVectors(vectors, Ct)).Value;
    }

    [Fact]
    public async Task An_empty_store_is_an_empty_matrix()
    {
        var matrix = (await _cache.Current(Model, Ct)).Value;

        Assert.Empty(matrix.Keys);
        Assert.Equal(0, matrix.Data.Length);
    }

    [Fact]
    public async Task Rows_are_the_stored_vectors_in_seq_order()
    {
        await Put("linear:issue/1", "v1", "one", "two");

        var matrix = (await _cache.Current(Model, Ct)).Value;

        Assert.Equal(["linear:issue/1#0", "linear:issue/1#1"], matrix.Keys);
        Assert.Equal(2, matrix.Dim);
        Assert.Equal(4, matrix.Data.Length);
    }

    [Fact]
    public async Task A_later_call_reads_only_the_rows_after_the_last_one_and_an_old_snapshot_does_not_change()
    {
        await Put("linear:issue/1", "v1", "one");
        var before = (await _cache.Current(Model, Ct)).Value;
        _store.VectorReads.Clear();

        await Put("linear:issue/2", "v1", "two");
        var after = (await _cache.Current(Model, Ct)).Value;

        Assert.Equal(["linear:issue/1#0"], before.Keys);
        Assert.Equal(["linear:issue/1#0", "linear:issue/2#0"], after.Keys);
        Assert.Equal(1L, _store.VectorReads[0]);
    }

    [Fact]
    public async Task An_unchanged_store_is_not_read_again()
    {
        await Put("linear:issue/1", "v1", "one");
        _ = await _cache.Current(Model, Ct);
        _store.VectorReads.Clear();

        _ = await _cache.Current(Model, Ct);

        Assert.Empty(_store.VectorReads);
    }

    [Fact]
    public async Task A_deleted_vector_leaves_the_matrix_at_the_next_call()
    {
        await Put("linear:issue/1", "v1", "one", "two");
        _ = await _cache.Current(Model, Ct);
        _store.VectorReads.Clear();

        _ = (await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [RecordKey.From("linear:issue/1")] }, Ct)).Value;
        var matrix = (await _cache.Current(Model, Ct)).Value;

        Assert.Empty(matrix.Keys);
        Assert.Equal(0L, _store.VectorReads[0]);
    }

    [Fact]
    public async Task A_reembedded_chunk_appears_once_with_its_new_vector()
    {
        await Put("linear:issue/1", "v1", "one");
        _ = await _cache.Current(Model, Ct);
        _ = (await _store.PutVectors([RecordSamples.Vector(new ChunkKey(RecordKey.From("linear:issue/1"), 0), "one", 0, 1)], Ct)).Value;

        var matrix = (await _cache.Current(Model, Ct)).Value;

        Assert.Equal(["linear:issue/1#0"], matrix.Keys);
        Assert.Equal([0f, 1f], matrix.Data.ToArray());
    }

    [Fact]
    public async Task Another_model_gets_its_own_matrix_and_other_models_rows_are_left_out()
    {
        await Put("linear:issue/1", "v1", "one");

        var other = (await _cache.Current("other-model@1", Ct)).Value;
        var mine = (await _cache.Current(Model, Ct)).Value;

        Assert.Empty(other.Keys);
        _ = Assert.Single(mine.Keys);
    }

    [Fact]
    public async Task Enough_rows_to_grow_the_arrays_keep_their_order_and_data()
    {
        var texts = Enumerable.Range(0, 1500).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"text {i}")).ToArray();
        await Put("linear:issue/1", "v1", texts);

        var matrix = (await _cache.Current(Model, Ct)).Value;

        Assert.Equal(1500, matrix.Keys.Count);
        Assert.Equal("linear:issue/1#1499", matrix.Keys[1499]);
        Assert.Equal(3000, matrix.Data.Length);
    }

    private sealed class CountingStore : IRecordStore
    {
        private readonly FakeRecordStore _inner = new();

        public List<long> VectorReads { get; } = [];

        public ValueTask<Kernel.Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken) => _inner.Commit(batch, cancellationToken);

        public ValueTask<Kernel.Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken) => _inner.Get(key, cancellationToken);

        public ValueTask<Kernel.Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken) => _inner.Chunks(key, cancellationToken);

        public ValueTask<Kernel.Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken) => _inner.List(filter, after, limit, cancellationToken);

        public ValueTask<Kernel.Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken) => _inner.Position(source, cancellationToken);

        public ValueTask<Kernel.Result<int>> RecordOutcome(SourceId source, SyncOutcome outcome, CancellationToken cancellationToken) => _inner.RecordOutcome(source, outcome, cancellationToken);

        public ValueTask<Kernel.Result<SyncOutcome?>> LastOutcome(SourceId source, CancellationToken cancellationToken) => _inner.LastOutcome(source, cancellationToken);

        public ValueTask<Kernel.Result<int>> RecordAttempt(SourceId source, SyncAttempt attempt, CancellationToken cancellationToken) => _inner.RecordAttempt(source, attempt, cancellationToken);

        public ValueTask<Kernel.Result<SourceBackoffState>> Backoff(SourceId source, CancellationToken cancellationToken) => _inner.Backoff(source, cancellationToken);

        public ValueTask<Kernel.Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken) => _inner.PutVectors(vectors, cancellationToken);

        public ValueTask<Kernel.Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken)
        {
            VectorReads.Add(afterSeq);
            return _inner.Vectors(space, afterSeq, limit, cancellationToken);
        }

        public ValueTask<Kernel.Result<IReadOnlyList<ChunkToEmbed>>> ChunksToEmbed(string model, int limit, CancellationToken cancellationToken) => _inner.ChunksToEmbed(model, limit, cancellationToken);

        public ValueTask<Kernel.Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken) => _inner.DeleteVectors(space, keys, cancellationToken);

        public ValueTask<Kernel.Result<StoreStamps>> Stamps(CancellationToken cancellationToken) => _inner.Stamps(cancellationToken);

        public ValueTask<Kernel.Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => _inner.Purge(source, cancellationToken);

        public ValueTask<Kernel.Result<LiveRecordCounts>> LiveCounts(CancellationToken cancellationToken) => _inner.LiveCounts(cancellationToken);

        public ValueTask<Kernel.Result<SourceCounts>> Count(SourceId source, CancellationToken cancellationToken) => _inner.Count(source, cancellationToken);
    }
}
