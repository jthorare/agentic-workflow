using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Turns the SQLite failures (primary result codes, https://sqlite.org/rescode.html) a caller should expect into <see cref="Error"/>s. Everything else stays an exception (a bug).</summary>
internal static class SqliteErrors
{
    internal const int SqlitePerm = 3;
    internal const int SqliteBusy = 5;
    internal const int SqliteReadOnly = 8;
    internal const int SqliteInterrupt = 9;
    internal const int SqliteIoErr = 10;
    internal const int SqliteCorrupt = 11;
    internal const int SqliteFull = 13;
    internal const int SqliteCantOpen = 14;
    internal const int SqliteConstraint = 19;
    internal const int SqliteNotADb = 26;

    /// <summary>Maps <paramref name="exception"/> when it is an expected failure.</summary>
    /// <param name="exception">The SQLite exception.</param>
    /// <param name="context">What was being done, for the message.</param>
    /// <param name="error">The mapped error.</param>
    /// <returns>True when the failure is expected; false when it is a bug the caller must rethrow.</returns>
    internal static bool TryMap(SqliteException exception, string context, out Error error)
    {
        var definition = (exception.SqliteErrorCode & 0xFF) switch
        {
            // SQLITE_LOCKED (6) is a conflict inside one connection: a bug, so it isn't mapped.
            SqliteBusy => DataErrors.Busy,
            SqliteConstraint => DataErrors.Constraint,
            SqliteCorrupt or SqliteNotADb => DataErrors.Damaged,
            SqlitePerm or SqliteReadOnly or SqliteIoErr or SqliteFull or SqliteCantOpen => DataErrors.Unavailable,
            _ => null,
        };
        error = definition?.With($"{context}: {exception.Message}")!; // why: only read when this returns true
        return definition is not null;
    }
}
