using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Tests.EmbedRecords;

/// <summary>A record store that takes no vector write (a guard that keeps refusing the same chunks), so every embed batch stores nothing.</summary>
internal sealed class VectorRefusingStore(IRecordStore inner) : IRecordStore
{
    public ValueTask<Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken) => inner.Commit(batch, cancellationToken);

    public ValueTask<Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken) => inner.Get(key, cancellationToken);

    public ValueTask<Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken) => inner.Chunks(key, cancellationToken);

    public ValueTask<Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken) => inner.List(filter, after, limit, cancellationToken);

    public ValueTask<Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken) => inner.Position(source, cancellationToken);

    public ValueTask<Result<int>> RecordOutcome(SourceId source, SyncOutcome outcome, CancellationToken cancellationToken) => inner.RecordOutcome(source, outcome, cancellationToken);

    public ValueTask<Result<SyncOutcome?>> LastOutcome(SourceId source, CancellationToken cancellationToken) => inner.LastOutcome(source, cancellationToken);

    public ValueTask<Result<int>> RecordAttempt(SourceId source, SyncAttempt attempt, CancellationToken cancellationToken) => inner.RecordAttempt(source, attempt, cancellationToken);

    public ValueTask<Result<SourceBackoffState>> Backoff(SourceId source, CancellationToken cancellationToken) => inner.Backoff(source, cancellationToken);

    public ValueTask<Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken) => ValueTask.FromResult<Result<int>>(0);

    public ValueTask<Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken) => inner.Vectors(space, afterSeq, limit, cancellationToken);

    public ValueTask<Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken) => inner.DeleteVectors(space, keys, cancellationToken);

    public ValueTask<Result<IReadOnlyList<ChunkToEmbed>>> ChunksToEmbed(string model, int limit, CancellationToken cancellationToken) => inner.ChunksToEmbed(model, limit, cancellationToken);

    public ValueTask<Result<StoreStamps>> Stamps(CancellationToken cancellationToken) => inner.Stamps(cancellationToken);

    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => inner.Purge(source, cancellationToken);

    public ValueTask<Result<LiveRecordCounts>> LiveCounts(CancellationToken cancellationToken) => inner.LiveCounts(cancellationToken);

    public ValueTask<Result<SourceCounts>> Count(SourceId source, CancellationToken cancellationToken) => inner.Count(source, cancellationToken);
}
