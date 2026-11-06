namespace Aw.Context.Core.Records;

/// <summary>
/// Rows a per-source writer puts in the store in the same transaction as a record (the generic option the Linear hub spec 2026-11-04 D4 and the PR review page spec 2026-11-04 §5.3 share).
/// The Linear writer comes next on this seam (Linear hub spec D4). The shared store knows only this base type: <see cref="Writer"/> names the writer that stores it. <c language="text">IRecordStore.Commit</c> hands the rows to that writer after the record row, writes
/// them again when they differ from what the writer stored even if the record itself is unchanged (which is how a new parser or a new field backfills with no new commit), and has every writer delete
/// a record's rows when the record is removed. A write with no side rows leaves the writers' rows alone, so a payload that means "none" must be sent explicitly.
/// </summary>
/// <param name="Writer">The id of the writer that stores these rows; a batch naming a writer the store does not have is invalid (SND-CONTEXT-230).</param>
public abstract record SideRows(string Writer);
