using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

internal sealed partial class SqliteRecordStore
{
    // The side rows (RecordWrite.Side) go in the record's transaction; every writer drops a removed record's rows.
    private async ValueTask<Error?> WriteSide(SqliteUnitOfWork session, RecordWrite write, CancellationToken cancellationToken) =>
        write.Side is { } side ? await Writer(side).Replace(session, write.Record.Key, side, cancellationToken).ConfigureAwait(false) : null;

    // An unchanged record still gets its side rows rewritten when they differ from the stored ones: a new parser or a new field backfills with no new commit.
    private async ValueTask<Result<bool>> WriteChangedSide(SqliteUnitOfWork session, RecordWrite write, CancellationToken cancellationToken)
    {
        if (write.Side is not { } side)
        {
            return false;
        }

        var writer = Writer(side);
        var differs = await writer.Differs(session, write.Record.Key, side, cancellationToken).ConfigureAwait(false);
        if (differs.IsError || !differs.Value)
        {
            return differs;
        }

        var failure = await writer.Replace(session, write.Record.Key, side, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        return true;
    }

    private async ValueTask<Error?> DeleteSide(SqliteUnitOfWork session, RecordKey key, CancellationToken cancellationToken)
    {
        foreach (var writer in _sideRowWriters)
        {
            if (await writer.Delete(session, key, cancellationToken).ConfigureAwait(false) is { } failure)
            {
                return failure;
            }
        }

        return null;
    }

    // Why a write's side rows can't be stored, or null.
    private string? SideProblem(RecordWrite write) =>
        write.Side is { } side && _sideRowWriters.All(w => !string.Equals(w.Id, side.Writer, StringComparison.Ordinal))
            ? $"record '{write.Record.Key.Value}' names side-row writer '{side.Writer}', which the store does not have"
            : null;

    private ISideRowWriter Writer(SideRows side) => _sideRowWriters.First(w => string.Equals(w.Id, side.Writer, StringComparison.Ordinal));
}
