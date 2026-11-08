using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>Where connections are kept ('connection' table in ledger.db). Rows hold no secret.</summary>
public interface IConnectionStore
{
    /// <summary>Reads one connection.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The connection, or SND-CONN-001.</returns>
    public ValueTask<Result<Connection>> Get(ConnectionName name, CancellationToken cancellationToken);

    /// <summary>Lists every connection.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The connections sorted by name.</returns>
    public ValueTask<Result<IReadOnlyList<Connection>>> List(CancellationToken cancellationToken);

    /// <summary>Inserts or replaces a connection by name.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or the store's error.</returns>
    public ValueTask<Result<Unit>> Upsert(Connection connection, CancellationToken cancellationToken);

    /// <summary>Deletes a connection.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when a row was deleted; false when there was none.</returns>
    public ValueTask<Result<bool>> Remove(ConnectionName name, CancellationToken cancellationToken);
}
