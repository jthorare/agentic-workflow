using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>
/// The ledger's read-only side in SQLite (API spec C9): read sessions only (busy_timeout and query_only), never
/// <see cref="LedgerDatabase.Writable"/>, so it never migrates or creates the file; a missing or unmigrated ledger
/// reads as empty with head 0.
/// </summary>
internal sealed class SqliteLedgerReader(LedgerDatabase database) : ILedgerReader
{
    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        Read(new SqlStatement(LedgerSql.SelectAfter, c => c.Bind("$seq", sequence).Bind("$max", Math.Max(max, 1))), LedgerEventMapper.Instance,
            cancellationToken);

    public async ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var head = await Head(cancellationToken).ConfigureAwait(false);   // before the rows: a row appended in between is only ever read twice, never skipped
        if (head.IsError)
        {
            return head.Error;
        }

        var rows = await Read(LedgerSql.ScanAfter(sequence, Math.Max(max, 1), filter), LedgerEventMapper.Instance, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : filter.Scanned(sequence, head.Value, max, rows.Value);
    }

    public async ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken)
    {
        var events = await Read(new SqlStatement(LedgerSql.SelectLatest, c => c.Bind("$type", type)), LedgerEventMapper.Instance, cancellationToken)
            .ConfigureAwait(false);
        return events.IsError ? events.Error : Results.Success(events.Value.Count == 0 ? null : events.Value[0]);
    }

    public async ValueTask<Result<LedgerEvent?>> ByKey(string idempotencyKey, CancellationToken cancellationToken)
    {
        var events = await Read(new SqlStatement(LedgerSql.SelectEventByKey, c => c.Bind("$key", idempotencyKey)), LedgerEventMapper.Instance, cancellationToken).ConfigureAwait(false);
        return events.IsError ? events.Error : Results.Success(events.Value.Count == 0 ? null : events.Value[0]);
    }

    public async ValueTask<Result<long>> Head(CancellationToken cancellationToken)
    {
        var head = await Read(new SqlStatement(LedgerSql.SelectHead), (IRowMapper<long>)CountMapper.Instance, cancellationToken).ConfigureAwait(false);
        return head.IsError ? head.Error : head.Value.Count == 0 ? 0L : head.Value[0];
    }

    private async ValueTask<Result<IReadOnlyList<T>>> Read<T>(SqlStatement statement, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        var schema = await database.Schema(cancellationToken).ConfigureAwait(false);
        if (schema.IsError || schema.Value.Applied == 0)
        {
            return schema.IsError ? schema.Error : Results.Success<IReadOnlyList<T>>([]);
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return LedgerDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, statement, mapper, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? LedgerDatabase.Translate(rows.Error) : rows;
        }
    }
}
