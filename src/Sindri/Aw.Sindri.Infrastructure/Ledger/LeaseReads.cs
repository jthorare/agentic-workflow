using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>Lease reads and the write-then-commit step the lease and the ledger share.</summary>
internal static class LeaseReads
{
    /// <summary>The lease row inside <paramref name="session"/>, or null when it was never held.</summary>
    /// <param name="session">A read or write session.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row.</returns>
    internal static async ValueTask<Result<LeaseRow?>> Current(ISqliteSession session, CancellationToken cancellationToken)
    {
        var rows = await SqliteQueries.List(session, new SqlStatement(LedgerSql.SelectLease), LeaseRowMapper.Instance, cancellationToken)
            .ConfigureAwait(false);
        return rows.IsError ? LedgerDatabase.Translate(rows.Error) : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    /// <summary>Runs <paramref name="statement"/> and commits the session.</summary>
    /// <param name="session">The write session.</param>
    /// <param name="statement">The write.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the translated store error.</returns>
    internal static async ValueTask<Result<Unit>> WriteAndCommit(SqliteUnitOfWork session, SqlStatement statement, CancellationToken cancellationToken)
    {
        var written = await SqliteQueries.Execute(session, statement, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return LedgerDatabase.Translate(written.Error);
        }

        var committed = await session.Commit(cancellationToken).ConfigureAwait(false);
        return committed.IsError ? LedgerDatabase.Translate(committed.Error) : Unit.Value;
    }
}
