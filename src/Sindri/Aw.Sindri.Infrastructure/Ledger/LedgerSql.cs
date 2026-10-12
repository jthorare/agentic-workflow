namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>The ledger's SQL. Engine-specific forms (ON CONFLICT upserts, RETURNING) stay here, named.</summary>
internal static class LedgerSql
{
    internal const string CountApplied = "SELECT COUNT(*) FROM schema_version";

    internal const string SelectLease =
        "SELECT host, pid, pid_start, boot_id, epoch, acquired_at, expires_at, released FROM lease WHERE id = 1";

    internal const string UpsertLease =
        "INSERT INTO lease (id, host, pid, pid_start, boot_id, epoch, acquired_at, expires_at, released) " +
        "VALUES (1, $host, $pid, $pidStart, $bootId, $epoch, $acquiredAt, $expiresAt, 0) " +
        "ON CONFLICT (id) DO UPDATE SET host = excluded.host, pid = excluded.pid, pid_start = excluded.pid_start, " +
        "boot_id = excluded.boot_id, epoch = excluded.epoch, acquired_at = excluded.acquired_at, " +
        "expires_at = excluded.expires_at, released = 0";

    internal const string RenewLease = "UPDATE lease SET expires_at = $expiresAt WHERE id = 1 AND epoch = $epoch AND released = 0";

    internal const string ReleaseLease = "UPDATE lease SET expires_at = $now, released = 1 WHERE id = 1 AND epoch = $epoch AND released = 0";

    private const string EventColumns =
        "seq, epoch, id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, causation_id, trace_id, " +
        "payload_json, idempotency_key, item";

    internal const string SelectByKey = "SELECT seq FROM events WHERE idempotency_key = $key";

    internal const string InsertEvent =
        "INSERT INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, item, correlation_id, causation_id, " +
        "trace_id, epoch, idempotency_key, payload_json) VALUES ($id, $type, $schemaVersion, $occurredAt, $actor, $tenant, " +
        "$ring, $item, $correlationId, $causationId, $traceId, $epoch, $key, $payload) RETURNING seq";

    internal const string SelectAfter = "SELECT " + EventColumns + " FROM events WHERE seq > $seq ORDER BY seq LIMIT $max";

    internal const string SelectLatest = "SELECT " + EventColumns + " FROM events WHERE type = $type ORDER BY seq DESC LIMIT 1";
}
