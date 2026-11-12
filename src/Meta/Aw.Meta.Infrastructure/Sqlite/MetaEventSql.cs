using Aw.Kernel.Data;
using Aw.Meta.Core.Events;
using Microsoft.Data.Sqlite;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>The event log's SQL.</summary>
internal static class MetaEventSql
{
    internal static SqlStatement Insert(MetaEvent metaEvent) => new(
        "INSERT INTO meta_events (at, kind, lane, fields) VALUES ($at, $kind, $lane, $fields)",
        c => c.Bind("$at", metaEvent.At).Bind("$kind", metaEvent.Kind).Bind("$lane", metaEvent.Lane).Bind("$fields", metaEvent.Fields),
        "event " + metaEvent.Kind);

    internal static readonly SqlStatement LastInsert = new("SELECT last_insert_rowid()", Subject: "event id");

    internal static SqlStatement Prune(DateTimeOffset olderThan) => new(
        "DELETE FROM meta_events WHERE at < $cutoff",
        c => c.Bind("$cutoff", olderThan),
        "events older than " + olderThan.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

    internal static SqlStatement Read(long after, string? kind, string? lane, int max) => new(
        "SELECT id, at, kind, lane, fields FROM meta_events WHERE id > $after AND ($kind IS NULL OR kind = $kind) AND ($lane IS NULL OR lane = $lane) ORDER BY id ASC LIMIT $max",
        c => c.Bind("$after", after).Bind("$kind", kind).Bind("$lane", lane).Bind("$max", max),
        "events after " + after.ToString(System.Globalization.CultureInfo.InvariantCulture));

    internal static MetaEvent ReadEvent(SqliteDataReader r) =>
        new(r.GetInt64(0), SqliteRead.GetTimestamp(r, 1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4));
}
