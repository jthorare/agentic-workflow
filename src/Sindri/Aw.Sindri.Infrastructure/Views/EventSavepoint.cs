using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>
/// The savepoint around one event's writes, opened at the first write (#2890): most events (a 'fleet.changed') write nothing, and an event
/// that only reads has nothing to roll back, so it costs no SAVEPOINT and RELEASE.
/// </summary>
/// <param name="transaction">The page transaction.</param>
/// <param name="name">The savepoint's name.</param>
internal sealed class EventSavepoint(SqliteTransaction transaction, string name)
{
    private bool _open;

    /// <summary>Opens the savepoint unless it is open already.</summary>
    internal async ValueTask Open(CancellationToken cancellationToken)
    {
        if (_open)
        {
            return;
        }

        await transaction.SaveAsync(name, cancellationToken).ConfigureAwait(false);
        _open = true;
    }

    /// <summary>Undoes the event's writes, when it made any.</summary>
    internal async ValueTask Rollback(CancellationToken cancellationToken)
    {
        if (_open)
        {
            await transaction.RollbackAsync(name, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Keeps the event's writes, when it made any.</summary>
    internal async ValueTask Release(CancellationToken cancellationToken)
    {
        if (_open)
        {
            await transaction.ReleaseAsync(name, cancellationToken).ConfigureAwait(false);
        }
    }
}
