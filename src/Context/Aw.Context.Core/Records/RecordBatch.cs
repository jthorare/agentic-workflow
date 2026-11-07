using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>
/// One page of one source, committed all or nothing (spec §5.5): the records and their chunks, the tombstones and the moved
/// position. A replay of a batch changes nothing: a record whose version and text hash are stored is left as it is.
/// </summary>
/// <param name="Source">The source the page came from; every record and removed key belongs to it.</param>
/// <param name="Records">Records to write.</param>
/// <param name="Removed">Records the source no longer has: they become tombstones.</param>
/// <param name="Position">The position after this page, written with it; null leaves the source's stored position as it is (a write-through of one record that is not a page of the feed).</param>
/// <param name="At">The driver's clock: a tombstone's 'RemovedAt'.</param>
/// <param name="Guard">When true, a record whose 'UpdatedAt' is strictly older than the stored one's is skipped (counted unchanged): a page fetched before a newer write cannot put the old value back. An equal 'UpdatedAt' passes, and so does a record with no time on either side.</param>
public sealed record RecordBatch(SourceId Source, IReadOnlyList<RecordWrite> Records, IReadOnlyList<RecordKey> Removed, SyncPosition? Position, DateTimeOffset At, bool Guard = false);
