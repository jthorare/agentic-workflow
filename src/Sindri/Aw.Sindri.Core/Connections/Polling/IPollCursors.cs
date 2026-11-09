using Aw.Kernel;

namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>Where poll cursors are kept ('poll_cursor' table in ledger.db).</summary>
public interface IPollCursors
{
    /// <summary>Reads one cursor.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="kind">The event kind.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The cursor, or null when none was saved (also on a file older than the table).</returns>
    public ValueTask<Result<PollCursor?>> Get(ConnectionName connection, EventKind kind, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the row of (connection, kind).</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or the store's error.</returns>
    public ValueTask<Result<Unit>> Save(PollCursor cursor, CancellationToken cancellationToken);

    /// <summary>Lists every cursor.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The cursors sorted by connection, then kind.</returns>
    public ValueTask<Result<IReadOnlyList<PollCursor>>> List(CancellationToken cancellationToken);
}
