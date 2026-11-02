using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Records;

/// <summary>
/// The store of source records, chunks and vectors (spec §4, §5.5): its write side is <see cref="Commit"/> for the sync
/// driver, its read side is what the map build and the search legs use. Every method answers an error, never throws, for a
/// store that can't be read ('SND-CONTEXT-004').
/// </summary>
public interface IRecordStore
{
    /// <summary>
    /// Commits one page in a single transaction: the records, their chunks and links, the tombstones and the position.
    /// A record whose version and text hash are already stored is left alone; a changed one has its chunks replaced and the
    /// vectors of the chunks that changed or went are dropped; a tombstone keeps the row and deletes its chunks and vectors.
    /// </summary>
    /// <param name="batch">The page.</param>
    /// <param name="cancellationToken">Cancels the commit; nothing is written then.</param>
    /// <returns>What it did; an invalid batch (a record of another source, a blocked record with chunks, gaps in 'Seq') is 'SND-CONTEXT-230' and writes nothing.</returns>
    public ValueTask<Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken);

    /// <summary>Reads one record.</summary>
    /// <param name="key">Its key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record, or null when the store has none (a tombstone is returned, with 'RemovedAt' set).</returns>
    public ValueTask<Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken);

    /// <summary>Reads a record's chunks.</summary>
    /// <param name="key">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Its chunks in 'Seq' order; none for an unknown, blocked or removed record.</returns>
    public ValueTask<Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken);

    /// <summary>Lists records in key order, a page at a time.</summary>
    /// <param name="filter">Which records.</param>
    /// <param name="after">The last key of the previous page, or null for the first.</param>
    /// <param name="limit">At most this many, 1 to 1,000.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page; an empty page ends the listing.</returns>
    public ValueTask<Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken);

    /// <summary>Reads a source's persisted place in its feed.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Its position, <see cref="SyncPosition.Start"/> when it never committed.</returns>
    public ValueTask<Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken);

    /// <summary>Records how a source's last tick ended, keeping its cursor and phase as they are (a fresh process reads it back through <see cref="LastOutcome"/>).</summary>
    /// <param name="source">The source.</param>
    /// <param name="outcome">When the tick ended and the code it ended with, null for success.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Nothing on success.</returns>
    public ValueTask<Result<int>> RecordOutcome(SourceId source, SyncOutcome outcome, CancellationToken cancellationToken);

    /// <summary>Reads how a source's last tick ended.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The outcome, or null when no tick of it was recorded.</returns>
    public ValueTask<Result<SyncOutcome?>> LastOutcome(SourceId source, CancellationToken cancellationToken);

    /// <summary>Records how a source's last tick went for its persisted backoff: a failure adds one to the failure count and sets the next try (5 minutes, doubling, at most 6 hours); a success resets both and sets the last commit. The cursor, phase and last outcome are kept as they are.</summary>
    /// <param name="source">The source.</param>
    /// <param name="attempt">How the tick ended.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Nothing on success.</returns>
    public ValueTask<Result<int>> RecordAttempt(SourceId source, SyncAttempt attempt, CancellationToken cancellationToken);

    /// <summary>Reads a source's persisted backoff and last commit.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state, <see cref="SourceBackoffState.None"/> for a source that never ticked.</returns>
    public ValueTask<Result<SourceBackoffState>> Backoff(SourceId source, CancellationToken cancellationToken);

    /// <summary>Stores vectors. A chunk vector is stored only while its chunk exists with the same text hash, so a batch that finishes after a re-chunk stores nothing for it; a vector for a key and model that has one replaces it with a new 'Seq'.</summary>
    /// <param name="vectors">The vectors.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many were stored.</returns>
    public ValueTask<Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken);

    /// <summary>Reads stored vectors after a position.</summary>
    /// <param name="space">Which space.</param>
    /// <param name="afterSeq">The highest 'Seq' already read, 0 for all.</param>
    /// <param name="limit">At most this many, 1 to 10,000.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Rows in 'Seq' order.</returns>
    public ValueTask<Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken);

    /// <summary>Lists chunks that have no vector for a model, or one made from other text, newest-updated record first (spec §5.6): the embed job's work list.</summary>
    /// <param name="model">The model name and digest.</param>
    /// <param name="limit">At most this many, 1 to 10,000.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Chunks of records that are neither blocked nor removed, records ordered by 'UpdatedAt' newest first (none last), then by key and 'Seq'; none when all are embedded.</returns>
    public ValueTask<Result<IReadOnlyList<ChunkToEmbed>>> ChunksToEmbed(string model, int limit, CancellationToken cancellationToken);

    /// <summary>Removes vectors by key (the map build retires a node), moving the delete epoch when any went.</summary>
    /// <param name="space">Which space.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many were removed.</returns>
    public ValueTask<Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken);

    /// <summary>Reads the change counters the caches watch.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The counters.</returns>
    public ValueTask<Result<StoreStamps>> Stamps(CancellationToken cancellationToken);

    /// <summary>Counts the live records (not removed, not blocked) and those with a chunk vector, in two queries, without paging the store.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The counts (zero for an empty store).</returns>
    public ValueTask<Result<LiveRecordCounts>> LiveCounts(CancellationToken cancellationToken);

    /// <summary>Counts what one source holds, so a purge can say what it is about to delete.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Its records, chunks and chunk vectors; all zero for a source the store never saw.</returns>
    public ValueTask<Result<SourceCounts>> Count(SourceId source, CancellationToken cancellationToken);

    /// <summary>Deletes everything one source gave: its records, links, chunks and vectors, and its position (the human's 'index purge').</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>How many records were deleted.</returns>
    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every stored project of a source the audience may see (not removed, not blocked), by title, each with its lead and its linked issues counted in SQL: open issues and the newest change among them.
    /// An issue counts only when its own audience is allowed too, so a count never reveals an issue the caller can't read.
    /// </summary>
    /// <param name="source">The source whose projects are read.</param>
    /// <param name="audience">The caller's audiences.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per project, ordered by title then key; none for a source with no projects.</returns>
    public ValueTask<Result<IReadOnlyList<ProjectRow>>> ProjectFacts(SourceId source, RecordAudience audience, CancellationToken cancellationToken);
}
