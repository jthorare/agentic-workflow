using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>The ledger's event log in SQLite: each append checks the lease epoch and the idempotency key in the same write transaction.</summary>
internal sealed class SqliteLedger(LedgerDatabase database) : ILedgerStore
{
    public async ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return LedgerDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var lease = await LeaseReads.Current(begun.Value, cancellationToken).ConfigureAwait(false);
            if (lease.IsError || !LeaseDecision.Holds(lease.Value, grant))
            {
                return lease.IsError ? lease.Error : SindriErrors.LeaseStale.With(string.Create(CultureInfo.InvariantCulture,
                    $"epoch {grant.Epoch} of {grant.Holder} is not current; nothing was recorded"));
            }

            return await InsertOnce(begun.Value, entry, grant, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<LedgerAppend>> InsertOnce(SqliteUnitOfWork session, LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var earlier = await SqliteQueries.List(session, new SqlStatement(LedgerSql.SelectByKey, c => c.Bind("$key", entry.IdempotencyKey)),
            (IRowMapper<long>)CountMapper.Instance, cancellationToken).ConfigureAwait(false);
        if (earlier.IsError || earlier.Value.Count > 0)
        {
            return earlier.IsError ? LedgerDatabase.Translate(earlier.Error) : new LedgerAppend(earlier.Value[0], AlreadyRecorded: true);
        }

        var inserted = await SqliteQueries.One(session, Insert(entry, grant), (IRowMapper<long>)CountMapper.Instance, cancellationToken)
            .ConfigureAwait(false);
        if (inserted.IsError)
        {
            return LedgerDatabase.Translate(inserted.Error);
        }

        var committed = await session.Commit(cancellationToken).ConfigureAwait(false);
        return committed.IsError ? LedgerDatabase.Translate(committed.Error) : new LedgerAppend(inserted.Value, AlreadyRecorded: false);
    }

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        ReadEvents(new SqlStatement(LedgerSql.SelectAfter, c => c.Bind("$seq", sequence).Bind("$max", Math.Max(max, 1))), cancellationToken);

    public async ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken)
    {
        var events = await ReadEvents(new SqlStatement(LedgerSql.SelectLatest, c => c.Bind("$type", type)), cancellationToken).ConfigureAwait(false);
        return events.IsError ? events.Error : Results.Success(events.Value.Count == 0 ? null : events.Value[0]);
    }

    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => database.Schema(cancellationToken);

    private async ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadEvents(SqlStatement statement, CancellationToken cancellationToken)
    {
        var schema = await database.Schema(cancellationToken).ConfigureAwait(false);
        if (schema.IsError || schema.Value.Applied == 0)
        {
            return schema.IsError ? schema.Error : Results.Success<IReadOnlyList<LedgerEvent>>([]);
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return LedgerDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, statement, LedgerEventMapper.Instance, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? LedgerDatabase.Translate(rows.Error) : rows;
        }
    }

    private static SqlStatement Insert(LedgerEntry entry, LeaseGrant grant)
    {
        var e = entry.Envelope;
        return new SqlStatement(LedgerSql.InsertEvent, c => c
            .Bind("$id", e.Id).Bind("$type", e.Type).Bind("$schemaVersion", e.SchemaVersion).Bind("$occurredAt", e.OccurredAt)
            .Bind("$actor", e.Actor).Bind("$tenant", e.Tenant).Bind("$ring", e.Ring).Bind("$item", entry.Item)
            .Bind("$correlationId", e.CorrelationId).Bind("$causationId", e.CausationId?.ToString("D"))
            .Bind("$traceId", e.TraceId).Bind("$epoch", grant.Epoch).Bind("$key", entry.IdempotencyKey).Bind("$payload", e.Payload),
            $"event {e.Type}");
    }
}
