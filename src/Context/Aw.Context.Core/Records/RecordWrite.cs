namespace Aw.Context.Core.Records;

/// <summary>One record and its chunks as a page commits them; a blocked record has no chunks.</summary>
/// <param name="Record">The record row.</param>
/// <param name="Chunks">Its chunks, 'Seq' 0 up without gaps.</param>
public sealed record RecordWrite(StoredRecord Record, IReadOnlyList<RecordChunk> Chunks);
