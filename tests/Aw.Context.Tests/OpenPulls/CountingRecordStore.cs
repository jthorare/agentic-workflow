using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Tests.OpenPulls;

/// <summary>A record store that delegates to <paramref name="inner"/> and counts the closed, merged and canceled rows its listings returned (#2154).</summary>
internal sealed class CountingRecordStore(IRecordStore inner) : IRecordStore
{
    private int _closedRowsRead;

    /// <summary>The rows in Closed, Merged or Canceled state that <see cref="List"/> returned.</summary>
    public int ClosedRowsRead => Volatile.Read(ref _closedRowsRead);

    public async ValueTask<Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken)
    {
        var page = await inner.List(filter, after, limit, cancellationToken).ConfigureAwait(false);
        if (!page.IsError)
        {
            _ = Interlocked.Add(ref _closedRowsRead, page.Value.Count(static r => r.State is RecordState.Closed or RecordState.Merged or RecordState.Canceled));
        }

        return page;
    }

    public ValueTask<Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken) => inner.Commit(batch, cancellationToken);

    public ValueTask<Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken) => inner.Get(key, cancellationToken);

    public ValueTask<Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken) => inner.Chunks(key, cancellationToken);

    public ValueTask<Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken) => inner.Position(source, cancellationToken);

    public ValueTask<Result<int>> RecordOutcome(SourceId source, SyncOutcome outcome, CancellationToken cancellationToken) => inner.RecordOutcome(source, outcome, cancellationToken);

    public ValueTask<Result<SyncOutcome?>> LastOutcome(SourceId source, CancellationToken cancellationToken) => inner.LastOutcome(source, cancellationToken);

    public ValueTask<Result<int>> RecordAttempt(SourceId source, SyncAttempt attempt, CancellationToken cancellationToken) => inner.RecordAttempt(source, attempt, cancellationToken);

    public ValueTask<Result<SourceBackoffState>> Backoff(SourceId source, CancellationToken cancellationToken) => inner.Backoff(source, cancellationToken);

    public ValueTask<Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken) => inner.PutVectors(vectors, cancellationToken);

    public ValueTask<Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken) => inner.Vectors(space, afterSeq, limit, cancellationToken);

    public ValueTask<Result<IReadOnlyList<ChunkToEmbed>>> ChunksToEmbed(string model, int limit, CancellationToken cancellationToken) => inner.ChunksToEmbed(model, limit, cancellationToken);

    public ValueTask<Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken) => inner.DeleteVectors(space, keys, cancellationToken);

    public ValueTask<Result<StoreStamps>> Stamps(CancellationToken cancellationToken) => inner.Stamps(cancellationToken);

    public ValueTask<Result<LiveRecordCounts>> LiveCounts(CancellationToken cancellationToken) => inner.LiveCounts(cancellationToken);

    public ValueTask<Result<SourceCounts>> Count(SourceId source, CancellationToken cancellationToken) => inner.Count(source, cancellationToken);

    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => inner.Purge(source, cancellationToken);

    public ValueTask<Result<IReadOnlyList<ProjectRow>>> ProjectFacts(SourceId source, RecordAudience audience, CancellationToken cancellationToken) => inner.ProjectFacts(source, audience, cancellationToken);
}
