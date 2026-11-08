using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Testing.Connections;

/// <summary>IConnectionStore in memory, keyed by the name's text. Held to <see cref="ConnectionStoreContract"/>.</summary>
public sealed class InMemoryConnectionStore : IConnectionStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Connection> _rows = new(StringComparer.Ordinal);

    /// <summary>Gets the stored connections, sorted by name (for assertions).</summary>
    public IReadOnlyList<Connection> Rows
    {
        get
        {
            lock (_gate)
            {
                return [.. _rows.Values.OrderBy(static c => c.Name.ToString(), StringComparer.Ordinal)];
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Connection>> Get(ConnectionName name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<Connection>>(_rows.TryGetValue(name.ToString(), out var found)
                ? found
                : SindriErrors.ConnectionNotFound.With($"No connection {name}."));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<Connection>>> List(CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<IReadOnlyList<Connection>>>(Rows.ToList());

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Upsert(Connection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        lock (_gate)
        {
            _rows[connection.Name.ToString()] = connection;
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc />
    public ValueTask<Result<bool>> Remove(ConnectionName name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<bool>>(_rows.Remove(name.ToString()));
        }
    }
}
