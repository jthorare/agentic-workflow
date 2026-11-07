using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// Runs statements on a session and returns <see cref="Result{T}"/>s. Expected failures (busy, constraint,
/// unavailable, no row) become AW-DATA errors; anything else (broken SQL, a mapper bug) throws.
/// </summary>
public static class SqliteQueries
{
    /// <summary>Reads one row: the first row the statement returns, or AW-DATA-001.</summary>
    /// <param name="session">The session.</param>
    /// <param name="statement">The query.</param>
    /// <param name="mapper">Maps the row.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <typeparam name="T">The row type.</typeparam>
    /// <returns>The mapped row or an error.</returns>
    public static async ValueTask<Result<T>> One<T>(ISqliteSession session, SqlStatement statement, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var command = Prepare(session, statement);
        await using var commandScope = command.ConfigureAwait(false);
        await using var interrupt = InterruptOnCancel(session, cancellationToken).ConfigureAwait(false);
        try
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? MapRow(mapper, reader, statement)
                : DataErrors.NotFound.With($"No row for {statement.Describe()}");
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == SqliteErrors.SqliteInterrupt && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(exception.Message, exception, cancellationToken);
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, statement.Describe(), out var error))
        {
            return error;
        }
    }

    /// <summary>Reads every row the statement returns, in order.</summary>
    /// <param name="session">The session.</param>
    /// <param name="statement">The query.</param>
    /// <param name="mapper">Maps each row.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <typeparam name="T">The row type.</typeparam>
    /// <returns>The rows (possibly none) or an error.</returns>
    public static async ValueTask<Result<IReadOnlyList<T>>> List<T>(ISqliteSession session, SqlStatement statement, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var command = Prepare(session, statement);
        await using var commandScope = command.ConfigureAwait(false);
        await using var interrupt = InterruptOnCancel(session, cancellationToken).ConfigureAwait(false);
        try
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            var rows = new List<T>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(MapRow(mapper, reader, statement));
            }

            return rows;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == SqliteErrors.SqliteInterrupt && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(exception.Message, exception, cancellationToken);
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, statement.Describe(), out var error))
        {
            return error;
        }
    }

    /// <summary>Reads the first column of the first row as a boolean (for 'SELECT EXISTS (…)').</summary>
    /// <param name="session">The session.</param>
    /// <param name="statement">The query.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>True when the value is a non-zero integer, false for 0, NULL or no row; or an error.</returns>
    public static async ValueTask<Result<bool>> Exists(ISqliteSession session, SqlStatement statement, CancellationToken cancellationToken)
    {
        var command = Prepare(session, statement);
        await using var commandScope = command.ConfigureAwait(false);
        await using var interrupt = InterruptOnCancel(session, cancellationToken).ConfigureAwait(false);
        try
        {
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is long number && number != 0;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == SqliteErrors.SqliteInterrupt && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(exception.Message, exception, cancellationToken);
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, statement.Describe(), out var error))
        {
            return error;
        }
    }

    /// <summary>Runs a statement that returns no rows.</summary>
    /// <param name="session">The session.</param>
    /// <param name="statement">The statement.</param>
    /// <param name="cancellationToken">Cancels it.</param>
    /// <returns>The number of rows changed, or an error.</returns>
    public static async ValueTask<Result<int>> Execute(ISqliteSession session, SqlStatement statement, CancellationToken cancellationToken)
    {
        var command = Prepare(session, statement);
        await using var commandScope = command.ConfigureAwait(false);
        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, statement.Describe(), out var error))
        {
            return error;
        }
    }

    // why: a mapper that throws on a stored value (a Vogen id parse, a bad enum word) would leave the store as that bare exception, and a host would classify it by type: Vogen reads as a bad request id (#2229). Every such
    // failure leaves the store as a StoredRowException instead; a SqliteException keeps its own mapping, and an InvalidDataException (SqliteRead's own refusals, judge's and the code index's mappers) already says
    // the row can't be read and keeps the message its callers show.
    private static T MapRow<T>(IRowMapper<T> mapper, SqliteDataReader reader, SqlStatement statement)
    {
        try
        {
            return mapper.Map(reader);
        }
        catch (Exception exception) when (exception is not (SqliteException or OperationCanceledException or StoredRowException or InvalidDataException))   // boundary: see the why above
        {
            throw new StoredRowException(table: null, column: null, rowKey: null, statement.Describe(), exception);
        }
    }

    // why: Microsoft.Data.Sqlite checks the token only between steps, and one step of an aggregate is the whole scan, so a
    // cancelled read would run to its end (#1951: hours); sqlite3_interrupt ends the running statement. Reads only: an
    // interrupted write inside a transaction rolls the whole transaction back.
    private static CancellationTokenRegistration InterruptOnCancel(ISqliteSession session, CancellationToken cancellationToken) =>
        cancellationToken.Register(static connection => SQLitePCL.raw.sqlite3_interrupt(((SqliteConnection)connection!).Handle), session.Connection);

    private static SqliteCommand Prepare(ISqliteSession session, SqlStatement statement)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(statement);
        var command = session.Connection.CreateCommand();
#pragma warning disable CA2100 // SqlStatement is the reviewed type for command text: a constant, with values bound as parameters
        command.CommandText = statement.Sql;
#pragma warning restore CA2100
        command.Transaction = session.Transaction;
        statement.Bind?.Invoke(command);
        return command;
    }
}
