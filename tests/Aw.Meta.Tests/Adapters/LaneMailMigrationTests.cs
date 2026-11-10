using Aw.Kernel.Data;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Infrastructure.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class LaneMailMigrationTests : IDisposable
{
    private readonly TempDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0003_upgrades_a_v2_database_keeps_its_rows_backs_it_up_and_lane_mail_works()
    {
        var options = MetaDatabase.OptionsFor(_state.Path);
        var clock = new FakeTimeProvider();
        var migrations = EmbeddedMigrations.Load(typeof(MetaDatabase).Assembly, MetaDatabase.ResourcePrefix).Value;
        var connections = new SqliteConnectionFactory(options);
        _ = await new MigrationRunner(connections, clock).Migrate(migrations, CancellationToken.None, targetVersion: 2);
        await using (var connection = (await connections.Open(CancellationToken.None)).Value)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO messages (id, from_socket, from_name, body, at, kind) VALUES ('m1', 's', 'peer', 'old message', '2026-10-15T05:00:00.0000000Z', 'Request')";
            _ = await insert.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var database = new MetaDatabase(options, clock);
        var messages = new SqliteMessageStore(database);
        var mail = new SqliteLaneMail(database);
        var seq = await mail.Send("api", "hello", clock.GetUtcNow(), CancellationToken.None);
        _ = await mail.Deliver([seq.Value], clock.GetUtcNow(), CancellationToken.None);
        var reply = await mail.Reply(new LaneIdentity("t1", "api"), "done", clock.GetUtcNow(), CancellationToken.None);

        var stored = (await messages.Messages(CancellationToken.None)).Value;
        Assert.Equal(["done", "old message"], stored.Select(m => m.Body).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal("lane:api:1", reply.Value.Value);
        Assert.Empty((await mail.Owed("api", CancellationToken.None)).Value);
        await using var check = (await connections.Open(CancellationToken.None)).Value;
        await using var version = check.CreateCommand();
        version.CommandText = "SELECT MAX(version) FROM schema_version";
        Assert.Equal(5L, await version.ExecuteScalarAsync(CancellationToken.None));
        Assert.True(File.Exists(options.DatabasePath + ".bak-v2"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0004_upgrades_a_v3_database_keeps_its_gate_as_untreed_and_stores_a_tree_and_stack()
    {
        var options = MetaDatabase.OptionsFor(_state.Path);
        var clock = new FakeTimeProvider();
        var migrations = EmbeddedMigrations.Load(typeof(MetaDatabase).Assembly, MetaDatabase.ResourcePrefix).Value;
        var connections = new SqliteConnectionFactory(options);
        _ = await new MigrationRunner(connections, clock).Migrate(migrations, CancellationToken.None, targetVersion: 3);
        await using (var connection = (await connections.Open(CancellationToken.None)).Value)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO gates (pr, run, head, main, scopes, verdict, at) VALUES (7, 1, 'old-head', 'old-main', '', 'Pass', '2026-10-15T05:00:00.0000000Z')";
            _ = await insert.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var gates = new SqliteGateStore(new MetaDatabase(options, clock));
        var old = (await gates.Latest(7, CancellationToken.None)).Value!;
        var saved = await gates.Save(new GateRecord(7, "new-head", "m", [], [], GateVerdict.Pass, clock.GetUtcNow()) { Tree = "t1", Stack = [new StackedPr(5, "h5")] }, CancellationToken.None);
        var latest = (await gates.Latest(7, CancellationToken.None)).Value!;

        Assert.Equal("old-head", old.Head);
        Assert.Null(old.Tree);
        Assert.Empty(old.Stack);
        Assert.Equal(2, saved.Value.Run);
        Assert.Equal("t1", latest.Tree);
        Assert.Equal([new StackedPr(5, "h5")], latest.Stack);
        Assert.True(File.Exists(options.DatabasePath + ".bak-v3"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0005_upgrades_a_v4_database_keeps_its_messages_unshown_and_marks_them_shown()
    {
        var options = MetaDatabase.OptionsFor(_state.Path);
        var clock = new FakeTimeProvider();
        var migrations = EmbeddedMigrations.Load(typeof(MetaDatabase).Assembly, MetaDatabase.ResourcePrefix).Value;
        var connections = new SqliteConnectionFactory(options);
        _ = await new MigrationRunner(connections, clock).Migrate(migrations, CancellationToken.None, targetVersion: 4);
        await using (var connection = (await connections.Open(CancellationToken.None)).Value)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO messages (id, from_socket, from_name, body, at, kind) VALUES ('m1', 's', 'peer', 'open message', '2026-10-15T05:00:00.0000000Z', 'Request');"
                + "INSERT INTO messages (id, from_socket, from_name, body, at, kind, resolution, resolution_reason) VALUES ('m2', 's', 'peer', 'dismissed message', '2026-10-15T05:01:00.0000000Z', 'Request', 'Dismissed', 'done')";
            _ = await insert.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var messages = new SqliteMessageStore(new MetaDatabase(options, clock));
        var upgraded = (await messages.Messages(CancellationToken.None)).Value.OrderBy(m => m.Id.Value, StringComparer.Ordinal).ToList();
        _ = await messages.MarkShown([upgraded[0].Id], clock.GetUtcNow(), CancellationToken.None);
        var marked = (await messages.Messages(CancellationToken.None)).Value.Single(m => m.Id == upgraded[0].Id);

        Assert.Equal(["open message", "dismissed message"], upgraded.Select(m => m.Body), StringComparer.Ordinal);
        Assert.Null(upgraded[0].Resolution);
        Assert.Equal(MessageResolutionKind.Dismissed, upgraded[1].Resolution?.Kind);
        Assert.All(upgraded, m => Assert.Null(m.ShownAt));
        Assert.Equal(clock.GetUtcNow(), marked.ShownAt);
        Assert.True(File.Exists(options.DatabasePath + ".bak-v4"));
    }

    public void Dispose() => _state.Dispose();
}
