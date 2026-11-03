using Aw.Context.Core.Records;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The live-record counts every index status read asks for.</summary>
internal sealed partial class SqliteRecordStore
{
    // why: every status read asks for these counts; concurrent readers share one read instead of stacking up (#1951)
    private readonly SingleFlight<Result<LiveRecordCounts>> _liveCounts = new();

    public ValueTask<Result<LiveRecordCounts>> LiveCounts(CancellationToken cancellationToken) =>
        _liveCounts.Run(read => ReadLiveCounts(read).AsTask(), cancellationToken);

    private async ValueTask<Result<LiveRecordCounts>> ReadLiveCounts(CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : new LiveRecordCounts(0, 0);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.CountLive), RecordMappers.Live, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows.Value[0];
        }
    }
}
