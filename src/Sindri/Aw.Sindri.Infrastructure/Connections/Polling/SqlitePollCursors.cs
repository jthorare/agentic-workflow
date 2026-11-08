using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IPollCursors over the 'poll_cursor' table of ledger.db (migration 10). A read never creates or migrates the file and finds
/// no cursor on a file older than the table.
/// </summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqlitePollCursors(LedgerDatabase database) : IPollCursors
{
    private readonly LedgerDatabase _database = database;

    public ValueTask<Result<PollCursor?>> Get(ConnectionName connection, EventKind kind, CancellationToken cancellationToken)
    {
        _ = _database;
        throw new NotSupportedException("red stub: PR 11 part 1");
    }

    public ValueTask<Result<Unit>> Save(PollCursor cursor, CancellationToken cancellationToken)
        => throw new NotSupportedException("red stub: PR 11 part 1");

    public ValueTask<Result<IReadOnlyList<PollCursor>>> List(CancellationToken cancellationToken)
        => throw new NotSupportedException("red stub: PR 11 part 1");
}
