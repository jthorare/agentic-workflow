using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// IConnectionStore over the 'connection' table of ledger.db (migration 8): non-secret config as JSON and the secret pointer's text.
/// A read never creates or migrates the file and finds no connection on a file older than the table.
/// </summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqliteConnectionStore(LedgerDatabase database) : IConnectionStore
{
    private readonly LedgerDatabase _database = database;

    public ValueTask<Result<Connection>> Get(ConnectionName name, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<IReadOnlyList<Connection>>> List(CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<Unit>> Upsert(Connection connection, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<bool>> Remove(ConnectionName name, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = _database;
        return new NotSupportedException();
    }
}
