using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IPollCursors over the 'poll_cursor' table of ledger.db (migration 10). A read never creates or migrates the file and finds
/// no cursor on a file older than the table.
/// </summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqlitePollCursors(LedgerDatabase database) : IPollCursors
{
    private const string Columns = "connection, event_kind, cursor, updated_at, last_poll_at, backoff_until, failures";

    private const string UpsertSql =
        "INSERT INTO poll_cursor (" + Columns + ") VALUES ($connection, $kind, $cursor, $updatedAt, $lastPollAt, $backoffUntil, $failures) " +
        "ON CONFLICT(connection, event_kind) DO UPDATE SET cursor = excluded.cursor, updated_at = excluded.updated_at, " +
        "last_poll_at = excluded.last_poll_at, backoff_until = excluded.backoff_until, failures = excluded.failures";

    private readonly LedgerDatabase _database = database;

    public ValueTask<Result<PollCursor?>> Get(ConnectionName connection, EventKind kind, CancellationToken cancellationToken) =>
        RecipeSessions.Read(_database, LedgerDatabase.PollCursorsVersion, empty: null,
            (session, token) => RecipeSessions.First(session,
                new SqlStatement("SELECT " + Columns + " FROM poll_cursor WHERE connection = $connection AND event_kind = $kind",
                    c => c.Bind("$connection", connection.ToString()).Bind("$kind", kind.Key), "poll cursor " + connection),
                PollCursorMapper.Instance, token),
            cancellationToken);

    public ValueTask<Result<Unit>> Save(PollCursor cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        return RecipeSessions.Write(_database, async ValueTask<Result<Unit>> (session, token) =>
        {
            var statement = new SqlStatement(UpsertSql, c => Bound(c, cursor), "poll cursor " + cursor.Connection);
            var written = await RecipeSessions.Execute(session, statement, token).ConfigureAwait(false);
            return written.IsError ? written.Error : Unit.Value;
        }, cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<PollCursor>>> List(CancellationToken cancellationToken) =>
        RecipeSessions.Read(_database, LedgerDatabase.PollCursorsVersion, [],
            (session, token) => RecipeSessions.List(session,
                new SqlStatement("SELECT " + Columns + " FROM poll_cursor ORDER BY connection, event_kind", Bind: null, Subject: "poll cursors"),
                PollCursorMapper.Instance, token),
            cancellationToken);

    private static SqliteCommand Bound(SqliteCommand command, PollCursor cursor) => command
        .Bind("$connection", cursor.Connection.ToString())
        .Bind("$kind", cursor.Kind.Key)
        .Bind("$cursor", cursor.Value)
        .Bind("$updatedAt", Text(cursor.UpdatedAt))
        .Bind("$lastPollAt", Text(cursor.LastPollAt))
        .Bind("$backoffUntil", Text(cursor.BackoffUntil))
        .Bind("$failures", cursor.Failures);

    private static string? Text(DateTimeOffset? value) => value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private sealed class PollCursorMapper : IRowMapper<PollCursor>
    {
        public static readonly PollCursorMapper Instance = new();

        public PollCursor Map(SqliteDataReader reader) => new(
            ConnectionName.Parse(reader.GetString(0)).Value,
            new EventKind(reader.GetString(1)),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            Timestamp(reader, 3),
            Timestamp(reader, 4),
            Timestamp(reader, 5),
            reader.GetInt32(6));

        private static DateTimeOffset? Timestamp(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : SqliteRead.GetTimestamp(reader, ordinal);
    }
}
