using System.Net.Sockets;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;
using Npgsql;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>
/// Reads a Postgres database through Npgsql as a role that can write nothing. Before any script query it checks who the server says it is
/// and what the role may do; the session then runs each statement in its own BEGIN READ ONLY transaction, on a connection whose
/// default transaction is read-only too.
/// </summary>
internal sealed class PostgresDatabaseReader : IDatabaseReader
{
    private const string IdentitySql = "SELECT coalesce(host(inet_server_addr()), ''), coalesce(inet_server_port(), 0), current_database()";

    private const string RoleSql =
        "SELECT r.rolsuper, r.rolcreaterole, r.rolcreatedb, r.rolreplication, r.rolbypassrls, "
        + "pg_has_role(current_user, 'pg_write_server_files', 'member'), "
        + "pg_has_role(current_user, 'pg_execute_server_program', 'member'), "
        + "(SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace "
        + "WHERE c.relkind IN ('r', 'p', 'v', 'm', 'f') AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\\_toast%' AND n.nspname NOT LIKE 'pg\\_temp\\_%' "
        + "AND (has_table_privilege(c.oid, 'INSERT') OR has_table_privilege(c.oid, 'UPDATE') OR has_table_privilege(c.oid, 'DELETE') OR has_table_privilege(c.oid, 'TRUNCATE'))), r.rolname "
        + "FROM pg_roles r WHERE r.rolname = current_user";

    /// <inheritdoc/>
    public async ValueTask<Result<IReadSession>> OpenAsync(DbTarget target, CancellationToken cancellationToken)
    {
        if (target is not PostgresTarget postgres)
        {
            return EvidenceErrors.DatabaseRefused.With("this reader opens Postgres servers only");
        }

        var connection = new NpgsqlConnection(PostgresConnectionString.Build(postgres.Entry, postgres.Password));
        var handedOver = false;
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var checks = await PreflightAsync(connection, postgres.Entry, cancellationToken).ConfigureAwait(false);
            if (checks.IsError)
            {
                return checks.Error;
            }

            handedOver = true;
            return new PostgresReadSession(connection);
        }
        catch (Exception exception) when (exception is NpgsqlException or SocketException or IOException or TimeoutException)
        {
            return EvidenceErrors.DatabaseUnavailable.With($"can't use {postgres.Entry.Host}:{postgres.Entry.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)} as {postgres.Entry.User}: {Describe(exception)}");
        }
        finally
        {
            if (!handedOver)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    // The server's own words, never the connection string: that holds the role's secret.
    private static string Describe(Exception exception) => exception is PostgresException server ? $"{server.SqlState} {server.MessageText}" : exception.GetBaseException().Message;

    private static async ValueTask<Result<Unit>> PreflightAsync(NpgsqlConnection connection, DatabaseEntry entry, CancellationToken cancellationToken)
    {
        var reported = await ReadIdentityAsync(connection, cancellationToken).ConfigureAwait(false);
        var identity = PostgresPreflight.CheckIdentity(entry.Identity, reported);
        if (identity.IsError)
        {
            return identity;
        }

        return PostgresPreflight.CheckRole(await ReadRoleAsync(connection, cancellationToken).ConfigureAwait(false));
    }

    private static async ValueTask<ReportedServer> ReadIdentityAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(IdentitySql, connection);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                _ = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var address = reader.GetString(0);
                var port = Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture);
                return new ReportedServer(address.Length == 0 ? null : address, port == 0 ? null : port, reader.GetString(2));
            }
        }
    }

    private static async ValueTask<RoleFacts> ReadRoleAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(RoleSql, connection);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                _ = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                return new RoleFacts(
                    reader.GetString(8), reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4),
                    reader.GetBoolean(5), reader.GetBoolean(6), Convert.ToInt32(reader.GetValue(7), System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }
}
