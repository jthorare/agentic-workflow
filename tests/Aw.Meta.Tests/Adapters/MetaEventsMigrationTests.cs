using Aw.Kernel.Data;
using Aw.Meta.Core.Events;
using Aw.Meta.Infrastructure.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class MetaEventsMigrationTests : IDisposable
{
    private readonly TempDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0006_upgrades_a_v5_database_keeps_its_rows_backs_it_up_and_events_work()
    {
        var options = MetaDatabase.OptionsFor(_state.Path);
        var clock = new FakeTimeProvider();
        var migrations = EmbeddedMigrations.Load(typeof(MetaDatabase).Assembly, MetaDatabase.ResourcePrefix).Value;
        var connections = new SqliteConnectionFactory(options);
        _ = await new MigrationRunner(connections, clock).Migrate(migrations, CancellationToken.None, targetVersion: 5);
        await using (var connection = (await connections.Open(CancellationToken.None)).Value)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO messages (id, from_socket, from_name, body, at, kind) VALUES ('m1', 's', 'peer', 'old message', '2026-10-15T05:00:00.0000000Z', 'Request')";
            _ = await insert.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var database = new MetaDatabase(options, clock);
        var events = new SqliteMetaEventStore(database);
        var id = await events.Append(new MetaEvent(0, clock.GetUtcNow(), "lane.down", "api", "{\"lane\":\"api\",\"reason\":\"idle\"}"), CancellationToken.None);
        var read = await events.Read(after: 0, kind: null, lane: null, max: 10, cancellationToken: CancellationToken.None);
        var messages = await new SqliteMessageStore(database).Messages(CancellationToken.None);

        Assert.Equal([id.Value], read.Value.Events.Select(e => e.Id));
        Assert.Equal("lane.down", read.Value.Events[0].Kind);
        Assert.Equal(["old message"], messages.Value.Select(m => m.Body), StringComparer.Ordinal);
        await using var check = (await connections.Open(CancellationToken.None)).Value;
        await using var version = check.CreateCommand();
        version.CommandText = "SELECT MAX(version) FROM schema_version";
        Assert.Equal(6L, await version.ExecuteScalarAsync(CancellationToken.None));
        Assert.True(File.Exists(options.DatabasePath + ".bak-v5"));
    }

    public void Dispose() => _state.Dispose();
}
