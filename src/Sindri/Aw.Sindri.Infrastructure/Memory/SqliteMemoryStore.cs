using Aw.Kernel;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Memory;

/// <summary>
/// IMemoryStore over the 'memory' and 'memory_fact' tables of ledger.db (migration 9): embeddings packed with Aw.Embeddings.VectorCodec, tags as sorted JSON.
/// A read never creates or migrates the file and finds nothing on a file older than the tables.
/// </summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqliteMemoryStore(LedgerDatabase database) : IMemoryStore
{
    private readonly LedgerDatabase _database = database;

    public ValueTask<Result<Unit>> Create(MemoryInfo memory, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<MemoryInfo>> Get(MemoryName name, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<IReadOnlyList<MemoryInfo>>> List(CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<IReadOnlyDictionary<string, int>>> Counts(CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<IReadOnlyList<MemoryFact>>> Facts(MemoryName name, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<Unit>> Add(MemoryFact fact, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<bool>> Refresh(MemoryName memory, string key, DateTimeOffset refreshedAt, DateTimeOffset? expiresAt, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<int>> Delete(MemoryName memory, FactSelector selector, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<int>> DeleteExpired(DateTimeOffset now, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = _database;
        return new NotSupportedException();
    }
}
