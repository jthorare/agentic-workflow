using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Tests.BuildMap;

/// <summary>A record store whose stamps can't be read, the first thing a map build asks it: the store file is gone or locked.</summary>
internal sealed class UnreadableRecordStore(IRecordStore inner) : IRecordStore
{
    public ValueTask<Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken) => inner.Commit(batch, cancellationToken);

    public ValueTask<Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken) => inner.Get(key, cancellationToken);

    public ValueTask<Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken) => inner.Chunks(key, cancellationToken);

    public ValueTask<Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken) => inner.List(filter, after, limit, cancellationToken);

    public ValueTask<Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken) => inner.Position(source, cancellationToken);

    public ValueTask<Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken) => inner.PutVectors(vectors, cancellationToken);

    public ValueTask<Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken) => inner.Vectors(space, afterSeq, limit, cancellationToken);

    public ValueTask<Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken) => inner.DeleteVectors(space, keys, cancellationToken);

    public ValueTask<Result<StoreStamps>> Stamps(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<StoreStamps>>(ContextErrors.StoreUnavailable.With("the record store can't be opened"));

    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => inner.Purge(source, cancellationToken);
}
