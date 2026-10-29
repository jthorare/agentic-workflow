using Aw.Kernel.Data;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>Appends many ledger rows in one write session (#1252): the lease check of a single append, then every insert and one commit, instead of one session per row.</summary>
internal static class BulkLedger
{
    /// <summary>Appends <paramref name="entries"/> in one transaction.</summary>
    /// <param name="database">The ledger database to write to.</param>
    /// <param name="grant">The lease the writer holds; it must still be current.</param>
    /// <param name="entries">The rows, in order.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The sequence of the last row appended, or 0 for none.</returns>
    internal static async Task<long> Append(LedgerDatabase database, LeaseGrant grant, IEnumerable<LedgerEntry> entries, CancellationToken ct)
    {
        var writable = (await database.Writable(ct)).Value;
        var begun = (await writable.BeginSession(ct)).Value;
        await using (begun.ConfigureAwait(false))
        {
            if (!LeaseDecision.Holds((await LeaseReads.Current(begun, ct)).Value, grant))
            {
                throw new InvalidOperationException("the host's lease is not current");
            }

            long last = 0;
            foreach (var entry in entries)
            {
                var e = entry.Envelope;
                var statement = new SqlStatement(LedgerSql.InsertEvent, c => c
                    .Bind("$id", e.Id).Bind("$type", e.Type).Bind("$schemaVersion", e.SchemaVersion).Bind("$occurredAt", e.OccurredAt)
                    .Bind("$actor", e.Actor).Bind("$tenant", e.Tenant).Bind("$ring", e.Ring).Bind("$item", entry.Item)
                    .Bind("$correlationId", e.CorrelationId).Bind("$causationId", e.CausationId?.ToString("D"))
                    .Bind("$traceId", e.TraceId).Bind("$epoch", grant.Epoch).Bind("$key", entry.IdempotencyKey).Bind("$payload", e.Payload).Bind("$temporalEventId", entry.TemporalEvent),
                    $"event {e.Type}");
                last = (await SqliteQueries.One(begun, statement, (IRowMapper<long>)CountMapper.Instance, ct)).Value;
            }

            _ = (await begun.Commit(ct)).Value;
            return last;
        }
    }
}
