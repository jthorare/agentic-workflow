using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>
/// The seam for rows a source keeps beside its records (<see cref="SideRows"/>). <c language="text">SqliteRecordStore.Commit</c> calls it inside the page's transaction, so a record and its side rows are never out of step.
/// A writer owns its tables and one payload type; it never touches <c language="text">source_record</c>.
/// </summary>
internal interface ISideRowWriter
{
    /// <summary>The id a <see cref="SideRows.Writer"/> names.</summary>
    public string Id { get; }

    /// <summary>Whether the stored rows differ from <paramref name="rows"/>; when true the rows are replaced even if the record is unchanged.</summary>
    public ValueTask<Result<bool>> Differs(ISqliteSession session, RecordKey key, SideRows rows, CancellationToken cancellationToken);

    /// <summary>Replaces the record's rows with <paramref name="rows"/>.</summary>
    public ValueTask<Error?> Replace(ISqliteSession session, RecordKey key, SideRows rows, CancellationToken cancellationToken);

    /// <summary>Deletes the record's rows (the record was removed); a no-op when it has none.</summary>
    public ValueTask<Error?> Delete(ISqliteSession session, RecordKey key, CancellationToken cancellationToken);
}
