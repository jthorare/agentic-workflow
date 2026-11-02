using Aw.Context.Infrastructure.Records;
using Aw.Context.Infrastructure.Store;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>A temp context.db with the real record store over it, and raw SQL beside it for what the port doesn't expose (planted triggers, row counts, the file's bytes).</summary>
internal sealed class RecordStoreWorld : IDisposable
{
    private readonly TempHome _home = new();

    public RecordStoreWorld()
    {
        Store = new SqliteRecordStore(_home.Database);
    }

    public SqliteRecordStore Store { get; }

    public ContextDatabase Database => _home.Database;

    public string DatabasePath => _home.Settings.DatabasePath;

    /// <summary>Creates and migrates the file, so raw SQL has tables to talk to.</summary>
    public async Task Migrate() => _ = await _home.Database.Writable(CancellationToken.None);

    /// <summary>Runs one statement outside the store.</summary>
    public void Sql(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    /// <summary>The first column of the first row of a query.</summary>
    public long Count(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose() => _home.Dispose();

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }
}
