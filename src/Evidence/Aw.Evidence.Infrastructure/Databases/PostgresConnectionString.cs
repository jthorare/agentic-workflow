using Aw.Evidence.Core.Data;
using Npgsql;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>The connection string for a Postgres entry: every field set explicitly, so no PG environment variable or password file applies.</summary>
internal static class PostgresConnectionString
{
    internal const int StatementTimeoutSeconds = 30;

    // Never read: the explicit login already decides, but a stray PGPASSFILE must not be consulted either.
    private const string NoPassfile = "/dev/null";

    /// <summary>Builds the connection string.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="login">The role's secret.</param>
    /// <returns>The connection string.</returns>
    internal static string Build(DatabaseEntry entry, string login) => new NpgsqlConnectionStringBuilder
    {
        Host = entry.Host,
        Port = entry.Port,
        Database = entry.Database,
        Username = entry.User,
        Password = login,
        Passfile = NoPassfile,
        SslMode = SslMode.Prefer,
        Options = $"-c default_transaction_read_only=on -c statement_timeout={StatementTimeoutSeconds * 1000}",
        ApplicationName = "ui-evidence db-run",
        Pooling = false,
        Timeout = 15,
        CommandTimeout = StatementTimeoutSeconds + 5,
    }.ConnectionString;
}
