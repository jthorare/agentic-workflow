using System.Globalization;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Microsoft.Data.Sqlite;

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
        "trace_id, epoch, idempotency_key, payload_json, temporal_event_id) VALUES ($id, $type, $schemaVersion, $occurredAt, $actor, $tenant, " +
        "$ring, $item, $correlationId, $causationId, $traceId, $epoch, $key, $payload, $temporalEventId) RETURNING seq";

    internal const string SelectAfter = "SELECT " + EventColumns + " FROM events WHERE seq > $seq ORDER BY seq LIMIT $max";

    /// <summary>
    /// The filtered read: rows after the cursor, no further than the scan limit, that the filter keeps, oldest first. Each glob is a
    /// bound parameter ('type = $t0' or "type LIKE $t1 ESCAPE '\'"); no text from a caller reaches the SQL string. The walk is over
    /// the primary key, so it needs no index. An instance filter is a key range on the idempotency_key's unique index
    /// ('workflow:{address}:' up to the next character after its ':'), so it reads the instance's rows alone; the planner may
    /// walk the index in key order, hence no row limit past the cursor and an ORDER BY seq on the few rows found.
    /// </summary>
    internal static SqlStatement ScanAfter(long sequence, int max, LedgerFilter filter)
    {
        var globs = filter.Types?.Globs ?? [];
        var types = globs.Count == 0
            ? string.Empty
            : " AND (" + string.Join(" OR ", globs.Select(static (g, i) => IsPrefix(g) ? "type LIKE " + TypeParam(i) + " ESCAPE '\\'" : "type = " + TypeParam(i))) + ")";
        var sql = "SELECT " + EventColumns + " FROM events WHERE " + Window(filter) + types +
            (filter.Item is null ? string.Empty : " AND item = $item") + " ORDER BY seq LIMIT $max";
        return new SqlStatement(sql, c =>
        {
            _ = c.Bind("$seq", sequence).Bind("$upper", filter.UpperFrom(sequence)).Bind("$max", max);
            if (filter.Item is { } item)
            {
                _ = c.Bind("$item", item.Value);
            }

            BindInstance(c, filter.Instance);

            for (var i = 0; i < globs.Count; i++)
            {
                _ = c.Bind(TypeParam(i), IsPrefix(globs[i]) ? EscapeLike(globs[i][..^1]) + "%" : globs[i]);
            }
        });
    }

    // why: with an instance the key range must drive the query, so the cursor is a unary-plus term (SQLite never uses an index for '+seq') and there is no upper bound; else the planner walks the primary key past every other instance's rows
    private static string Window(LedgerFilter filter) => filter.Instance is null
        ? "seq > $seq AND seq <= $upper"
        : "+seq > $seq AND idempotency_key >= $keyFrom AND idempotency_key < $keyTo";

    private static void BindInstance(SqliteCommand command, InstanceAddress? instance)
    {
        if (instance is { } address)
        {
            var prefix = LedgerFilter.InstanceKeyPrefix(address);
            _ = command.Bind("$keyFrom", prefix).Bind("$keyTo", string.Concat(prefix.AsSpan(0, prefix.Length - 1), ";"));   // ';' is the character after ':'
        }
    }

    private static string TypeParam(int index) => "$t" + index.ToString(CultureInfo.InvariantCulture);

    private static bool IsPrefix(string glob) => glob.EndsWith(".*", StringComparison.Ordinal);

    // why: defence in depth: TypeFilter.Parse admits only lower-case dotted names (no '%', '_' or '\\'), so a parsed glob never needs this today
    private static string EscapeLike(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    internal const string SelectHead = "SELECT COALESCE(MAX(seq), 0) FROM events";

    /// <summary>The 'instance' a recorded event's payload names, by envelope id (engine P4 unit (d), the bridge's causation route); SQLite's json_* functions.</summary>
    internal const string SelectInstanceOfEvent =
        "SELECT json_extract(payload_json, '$.instance') FROM events " +
        "WHERE id = $id AND json_valid(payload_json) AND json_type(payload_json, '$.instance') = 'text'";

    internal const string SelectLatest = "SELECT " + EventColumns + " FROM events WHERE type = $type ORDER BY seq DESC LIMIT 1";
}
