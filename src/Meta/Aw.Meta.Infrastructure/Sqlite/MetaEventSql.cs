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

    // AUTOINCREMENT keeps the highest id ever assigned in sqlite_sequence, so it survives a prune that empties the table.
    internal static readonly SqlStatement Bounds = new(
        "SELECT (SELECT MIN(id) FROM meta_events), COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'meta_events'), 0)",
        Subject: "event bounds");

    internal static (long Oldest, long Latest) ReadBounds(SqliteDataReader r)
    {
        var latest = r.GetInt64(1);
        return (r.IsDBNull(0) ? latest + 1 : r.GetInt64(0), latest);
    }

    internal static MetaEvent ReadEvent(SqliteDataReader r) =>
        new(r.GetInt64(0), SqliteRead.GetTimestamp(r, 1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4));
}
