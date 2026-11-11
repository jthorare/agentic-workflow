using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>
/// The savepoint around one event's writes to views.db (#2890). An event's rows are staged in its <see cref="PageRows"/>, so it opens only
/// when a Where query needs them written mid-page; most events never reach views.db before the page ends and cost no SAVEPOINT and RELEASE.
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
