using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// A session that keeps one prepared command per SQL text, so a statement run many times on it is parsed once.
/// <see cref="SqliteQueries"/> reuses its commands when handed it as the session, and rebinds the parameters on each run.
/// Wrap a session for a burst of the same few statements (the projector's page of 1,000 events: #2890); dispose it before the inner session.
/// Not thread-safe: one statement at a time, as on any one connection.
/// </summary>
/// <param name="inner">The session whose connection and transaction the commands run on.</param>
public sealed class SqliteStatementCache(ISqliteSession inner) : ISqliteSession, IDisposable
{
    private readonly ISqliteSession _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly Dictionary<string, SqliteCommand> _commands = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public SqliteConnection Connection => _inner.Connection;

    /// <inheritdoc />
    public SqliteTransaction? Transaction => _inner.Transaction;

    /// <summary>How many distinct statements it holds.</summary>
    public int Count => _commands.Count;

    /// <summary>Disposes every command it holds.</summary>
    public void Dispose()
    {
        foreach (var command in _commands.Values)
        {
            command.Dispose();
        }

        _commands.Clear();
    }

    /// <summary>The command for <paramref name="sql"/>, its parameters cleared: the one made on first use, then the same one each time.</summary>
    internal SqliteCommand Rent(string sql)
    {
        if (!_commands.TryGetValue(sql, out var command))
        {
            command = Connection.CreateCommand();
#pragma warning disable CA2100 // SqlStatement is the reviewed type for command text: a constant, with values bound as parameters
            command.CommandText = sql;
#pragma warning restore CA2100
            command.Transaction = Transaction;
            _commands.Add(sql, command);
        }

        command.Parameters.Clear();
        return command;
    }
}
