using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Tests.GetLenses;

/// <summary>A record store whose reads all fail, for the answers that must not fail with it.</summary>
internal sealed class FailingRecordStore : IRecordStore
{
    private static readonly Error _error = Aw.Context.Core.ContextErrors.QueryInvalid.With("the store can't be read");

    public ValueTask<Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken) => ValueTask.FromResult<Result<StoredRecord?>>(_error);

    public ValueTask<Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<int>> RecordOutcome(SourceId source, SyncOutcome outcome, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<SyncOutcome?>> LastOutcome(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<int>> RecordAttempt(SourceId source, SyncAttempt attempt, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<SourceBackoffState>> Backoff(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<ChunkToEmbed>>> ChunksToEmbed(string model, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<StoreStamps>> Stamps(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<LiveRecordCounts>> LiveCounts(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<SourceCounts>> Count(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<ProjectRow>>> ProjectFacts(SourceId source, RecordAudience audience, CancellationToken cancellationToken) => throw new NotSupportedException();
}
