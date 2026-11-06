namespace Aw.Context.Core.Records;

/// <summary>One record and its chunks as a page commits them; a blocked record has no chunks.</summary>
/// <param name="Record">The record row.</param>
/// <param name="Chunks">Its chunks, 'Seq' 0 up without gaps.</param>
/// <param name="Side">Rows a per-source writer stores with the record in the same transaction (<see cref="SideRows"/>); null leaves the writers' rows for this record alone.</param>
public sealed record RecordWrite(StoredRecord Record, IReadOnlyList<RecordChunk> Chunks, SideRows? Side = null);
