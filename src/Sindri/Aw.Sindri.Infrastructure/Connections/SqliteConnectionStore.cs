using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// IConnectionStore over the 'connection' table of ledger.db (migration 8): non-secret config as JSON and the secret pointer's text.
/// A read never creates or migrates the file and finds no connection on a file older than the table.
/// </summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqliteConnectionStore(LedgerDatabase database) : IConnectionStore
{
    private const string Columns = "name, kind, preset, config_json, secret_pointer, status, status_reason, created_by, created_at, updated_at, last_refreshed_at";

    private const string UpsertSql =
        "INSERT INTO connection (" + Columns + ") VALUES ($name, $kind, $preset, $config, $pointer, $status, $reason, $createdBy, $createdAt, $updatedAt, $refreshedAt) " +
        "ON CONFLICT(name) DO UPDATE SET kind = excluded.kind, preset = excluded.preset, config_json = excluded.config_json, secret_pointer = excluded.secret_pointer, " +
        "status = excluded.status, status_reason = excluded.status_reason, created_by = excluded.created_by, created_at = excluded.created_at, " +
        "updated_at = excluded.updated_at, last_refreshed_at = excluded.last_refreshed_at";

    public async ValueTask<Result<Connection>> Get(ConnectionName name, CancellationToken cancellationToken)
    {
        var found = await RecipeSessions.Read(database, LedgerDatabase.ConnectionsVersion, empty: null,
            (session, token) => RecipeSessions.First(session, new SqlStatement("SELECT " + Columns + " FROM connection WHERE name = $name", c => c.Bind("$name", name.ToString()), "connection " + name), ConnectionMapper.Instance, token),
            cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return found.Value is null ? SindriErrors.ConnectionNotFound.With($"no connection named {name}") : found.Value;
    }

    public ValueTask<Result<IReadOnlyList<Connection>>> List(CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, LedgerDatabase.ConnectionsVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement("SELECT " + Columns + " FROM connection ORDER BY name", Bind: null, Subject: "connections"), ConnectionMapper.Instance, token),
            cancellationToken);

    public ValueTask<Result<Unit>> Upsert(Connection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return RecipeSessions.Write(database, async ValueTask<Result<Unit>> (session, token) =>
        {
            var statement = new SqlStatement(UpsertSql, c => Bound(c, connection), "connection " + connection.Name);
            var written = await RecipeSessions.Execute(session, statement, token).ConfigureAwait(false);
            return written.IsError ? written.Error : Unit.Value;
        }, cancellationToken);
    }

    public ValueTask<Result<bool>> Remove(ConnectionName name, CancellationToken cancellationToken) =>
        RecipeSessions.Write(database, async ValueTask<Result<bool>> (session, token) =>
        {
            var removed = await RecipeSessions.Execute(session, new SqlStatement("DELETE FROM connection WHERE name = $name", c => c.Bind("$name", name.ToString()), "connection " + name), token).ConfigureAwait(false);
            return removed.IsError ? removed.Error : removed.Value > 0;
        }, cancellationToken);

    private static SqliteCommand Bound(SqliteCommand command, Connection connection) => command
        .Bind("$name", connection.Name.ToString())
        .Bind("$kind", ConnectionWire.Name(connection.Kind))
        .Bind("$preset", connection.Preset)
        .Bind("$config", ConfigJson(connection.Config))
        .Bind("$pointer", connection.SecretPointer?.ToString())
        .Bind("$status", ConnectionWire.Name(connection.Status))
        .Bind("$reason", connection.StatusReason)
        .Bind("$createdBy", connection.CreatedBy)
        .Bind("$createdAt", connection.CreatedAt)
        .Bind("$updatedAt", connection.UpdatedAt)
        .Bind("$refreshedAt", connection.LastRefreshedAt is { } refreshed ? refreshed.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) : null);

    private static string ConfigJson(IReadOnlyDictionary<string, string> config)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var pair in config.OrderBy(static p => p.Key, StringComparer.Ordinal))
            {
                writer.WriteString(pair.Key, pair.Value);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private sealed class ConnectionMapper : IRowMapper<Connection>
    {
        public static readonly ConnectionMapper Instance = new();

        public Connection Map(SqliteDataReader reader)
        {
            var config = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var document = JsonDocument.Parse(reader.GetString(3)))
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    config[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }

            var pointer = reader.IsDBNull(4) ? null : SecretPointer.Parse(reader.GetString(4)) is { IsSuccess: true } parsed ? parsed.Value : null;
            return new Connection(
                ConnectionName.Parse(reader.GetString(0)).Value,
                ConnectionWire.ParseKind(reader.GetString(1)).Value,
                reader.IsDBNull(2) ? null : reader.GetString(2),
                config,
                pointer,
                ConnectionWire.ParseStatus(reader.GetString(5)).Value,
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                SqliteRead.GetTimestamp(reader, 8),
                SqliteRead.GetTimestamp(reader, 9),
                reader.IsDBNull(10) ? null : SqliteRead.GetTimestamp(reader, 10));
        }
    }
}
