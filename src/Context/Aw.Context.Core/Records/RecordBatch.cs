using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>
/// One page of one source, committed all or nothing (spec §5.5): the records and their chunks, the tombstones and the moved
/// position. A replay of a batch changes nothing: a record whose version and text hash are stored is left as it is.
/// </summary>
/// <param name="Source">The source the page came from; every record and removed key belongs to it.</param>
/// <param name="Records">Records to write.</param>
/// <param name="Removed">Records the source no longer has: they become tombstones.</param>
/// <param name="Position">The position after this page, written with it.</param>
/// <param name="At">The driver's clock: a tombstone's 'RemovedAt'.</param>
public sealed record RecordBatch(SourceId Source, IReadOnlyList<RecordWrite> Records, IReadOnlyList<RecordKey> Removed, SyncPosition Position, DateTimeOffset At);
