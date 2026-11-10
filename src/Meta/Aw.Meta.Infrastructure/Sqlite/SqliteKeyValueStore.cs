using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>IKeyValueStore over meta.db (the kv table).</summary>
/// <param name="database">The meta database.</param>
internal sealed class SqliteKeyValueStore(MetaDatabase database) : IKeyValueStore
{
    public async ValueTask<Result<string?>> Get(string key, CancellationToken cancellationToken)
    {
        var rows = await database.Query(
            new SqlStatement("SELECT value FROM kv WHERE key = $key", c => c.Bind("$key", key), "kv " + key),
            r => r.GetString(0),
            cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    public ValueTask<Result<Unit>> Set(string key, string value, CancellationToken cancellationToken) =>
        database.Run(
            new SqlStatement(
                "INSERT INTO kv (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value",
                c => c.Bind("$key", key).Bind("$value", value),
                "kv " + key),
            cancellationToken);

    public ValueTask<Result<bool>> SetIf(string key, string? expected, string value, CancellationToken cancellationToken) =>
        database.Write(
            async work =>
            {
                // One statement. A null expected is an insert that does nothing when the key exists; otherwise an update that needs the stored value to still match.
                var statement = expected is null
                    ? new SqlStatement(
                        "INSERT INTO kv (key, value) VALUES ($key, $value) ON CONFLICT(key) DO NOTHING",
                        c => c.Bind("$key", key).Bind("$value", value),
                        "kv " + key)
                    : new SqlStatement(
                        "UPDATE kv SET value = $value WHERE key = $key AND value = $expected",
                        c => c.Bind("$key", key).Bind("$value", value).Bind("$expected", expected),
                        "kv " + key);
                var changed = await SqliteQueries.Execute(work, statement, cancellationToken).ConfigureAwait(false);
                return changed.IsError ? changed.Error : Results.Success(changed.Value > 0);
            },
            cancellationToken);
}
