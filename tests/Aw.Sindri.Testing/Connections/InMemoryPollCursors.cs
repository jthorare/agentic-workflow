using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Testing.Connections;

/// <summary>IPollCursors in memory, keyed by connection and event kind. Held to <see cref="PollCursorsContract"/>.</summary>
public sealed class InMemoryPollCursors : IPollCursors
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, PollCursor> _rows = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<Result<PollCursor?>> Get(ConnectionName connection, EventKind kind, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<PollCursor?>>(_rows.GetValueOrDefault(Key(connection, kind)));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Save(PollCursor cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        lock (_gate)
        {
            _rows[Key(cursor.Connection, cursor.Kind)] = cursor;
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<PollCursor>>> List(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var sorted = _rows.Values.OrderBy(static c => c.Connection.ToString(), StringComparer.Ordinal).ThenBy(static c => c.Kind.Key, StringComparer.Ordinal).ToList();
            return ValueTask.FromResult<Result<IReadOnlyList<PollCursor>>>(sorted);
        }
    }

    private static string Key(ConnectionName connection, EventKind kind) => connection + "|" + kind.Key;
}
