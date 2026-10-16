using Aw.Evidence.Core.Data;
using Aw.Evidence.Infrastructure.Databases;
using Npgsql;

namespace Aw.Evidence.Tests.Infrastructure;

public sealed class PostgresConnectionStringTests
{
    private const string Secret = "value-for-this-test-only";

    private static readonly DatabaseEntry _entry = new("postgres", "127.0.0.1", 55432, "fixture", "evidence_ro", "EVIDENCE_PG_SECRET", Disposable: true, new ServerIdentity("172.17.0.2", "fixture"));

    private static NpgsqlConnectionStringBuilder Built() => new(PostgresConnectionString.Build(_entry, Secret));

    [Fact, Trait("Lane", "unit")]
    public void Postgres_connection_ignores_PG_environment_defaults()
    {
        var built = Built();

        Assert.Equal("127.0.0.1", built.Host);
        Assert.Equal(55432, built.Port);
        Assert.Equal("fixture", built.Database);
        Assert.Equal("evidence_ro", built.Username);
        Assert.Equal(Secret, built.Password);
        Assert.False(string.IsNullOrEmpty(built.Passfile));
        Assert.Equal(SslMode.Prefer, built.SslMode);
        Assert.False(string.IsNullOrEmpty(built.Options));
        Assert.False(string.IsNullOrEmpty(built.ApplicationName));
        foreach (var key in new[] { "Host", "Port", "Database", "Username", "Password", "Passfile", "SSL Mode", "Options" })
        {
            Assert.Contains(key + "=", built.ConnectionString, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Postgres_connection_is_read_only_at_the_connection_with_a_statement_timeout_and_no_pooling()
    {
        var built = Built();

        Assert.Contains("default_transaction_read_only=on", built.Options, StringComparison.Ordinal);
        Assert.Contains("statement_timeout=", built.Options, StringComparison.Ordinal);
        Assert.False(built.Pooling);
    }
}
