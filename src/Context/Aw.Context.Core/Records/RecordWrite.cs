using Aw.Context.Core.Library;

namespace Aw.Context.Core.Records;

/// <summary>One record and its chunks as a page commits them; a blocked record has no chunks.</summary>
/// <param name="Record">The record row.</param>
/// <param name="Chunks">Its chunks, 'Seq' 0 up without gaps.</param>
/// <param name="Doc">What <see cref="DesignDocParser"/> made of a design doc record (spec 2026-11-04 §5.3); null when the record is not a design doc; on a doc record the store then clears any items it kept before.</param>
/// <param name="Claims">The refs a PR body's <c language="text">Covers:</c> line names (§4.4), empty when it names none; null on a PR means none; the store clears the claims it kept before. Any other record ignores it.</param>
public sealed record RecordWrite(StoredRecord Record, IReadOnlyList<RecordChunk> Chunks, ParsedDesignDoc? Doc = null, IReadOnlyList<string>? Claims = null);
