using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The registry's sessions on ledger.db: a write migrates the file and commits once the body succeeds; a read never
/// creates or migrates the file, and finds nothing on a file older than the registry. Store failures become
/// SND-LEDGER-001/002 (<see cref="LedgerDatabase.Translate"/>).
/// </summary>
internal static class RecipeSessions
{
    /// <summary>Runs <paramref name="write"/> in one write transaction and commits it when it succeeds.</summary>
    internal static async ValueTask<Result<T>> Write<T>(LedgerDatabase database, Func<SqliteUnitOfWork, CancellationToken, ValueTask<Result<T>>> write, CancellationToken cancellationToken)
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
            var written = await write(begun.Value, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? LedgerDatabase.Translate(committed.Error) : written;
        }
    }

    /// <summary>Runs <paramref name="read"/> in a read session, or returns <paramref name="empty"/> when the file has no registry yet.</summary>
    internal static ValueTask<Result<T>> Read<T>(LedgerDatabase database, T empty, Func<ISqliteSession, CancellationToken, ValueTask<Result<T>>> read, CancellationToken cancellationToken) =>
        Read(database, LedgerDatabase.RecipesVersion, empty, read, cancellationToken);

    /// <summary>Runs <paramref name="read"/> in a read session, or returns <paramref name="empty"/> when the file is older than migration <paramref name="since"/>.</summary>
    internal static async ValueTask<Result<T>> Read<T>(
        LedgerDatabase database, int since, T empty, Func<ISqliteSession, CancellationToken, ValueTask<Result<T>>> read, CancellationToken cancellationToken)
    {
        var schema = await database.Schema(cancellationToken).ConfigureAwait(false);
        if (schema.IsError)
        {
            return schema.Error;
        }

        if (schema.Value.Applied < since)
        {
            return empty;
        }

        var begun = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return LedgerDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            return await read(begun.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Every row the statement returns.</summary>
    internal static async ValueTask<Result<IReadOnlyList<T>>> List<T>(ISqliteSession session, SqlStatement statement, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        var rows = await SqliteQueries.List(session, statement, mapper, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? LedgerDatabase.Translate(rows.Error) : rows;
    }

    /// <summary>The first row the statement returns, or null when it returns none.</summary>
    internal static async ValueTask<Result<T?>> First<T>(ISqliteSession session, SqlStatement statement, IRowMapper<T> mapper, CancellationToken cancellationToken)
        where T : class
    {
        var rows = await List(session, statement, mapper, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    /// <summary>Whether a 'SELECT EXISTS (…)' holds.</summary>
    internal static async ValueTask<Result<bool>> Exists(ISqliteSession session, SqlStatement statement, CancellationToken cancellationToken)
    {
        var exists = await SqliteQueries.Exists(session, statement, cancellationToken).ConfigureAwait(false);
        return exists.IsError ? LedgerDatabase.Translate(exists.Error) : exists;
    }

    /// <summary>Runs a statement that returns no rows.</summary>
    internal static async ValueTask<Result<int>> Execute(ISqliteSession session, SqlStatement statement, CancellationToken cancellationToken)
    {
        var changed = await SqliteQueries.Execute(session, statement, cancellationToken).ConfigureAwait(false);
        return changed.IsError ? LedgerDatabase.Translate(changed.Error) : changed;
    }
}
