using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Intent;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Tests.Map;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linking;

/// <summary>Migration 0027 widens intent_link's rule list with 'alias' (integration, a temp home).</summary>
public sealed class IntentLinkAliasMigrationTests : IDisposable
{
    private static readonly RecordKey _r103 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TempHome _home = new();
    private readonly ContextStoreSettings _settings;
    private readonly ContextDatabase _database;

    public IntentLinkAliasMigrationTests()
    {
        _settings = SettingsFromEnvironment.Read(_home.Environment);
        _database = new ContextDatabase(_settings, new FakeTimeProvider());
    }

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_store_made_before_0027_keeps_its_name_link_takes_an_alias_link_and_the_stamp_triggers_still_fire()
    {
        var connections = new SqliteConnectionFactory(new SqliteOptions(_settings.DatabasePath));
        Directory.CreateDirectory(_settings.Home);
        Assert.True((await new MigrationRunner(connections, new FakeTimeProvider()).Migrate(ContextDatabase.Migrations, Ct, targetVersion: 26)).IsSuccess);
        await Execute("INSERT INTO intent_record (key, source, version, title, aliases, fetched_at, extraction_state) VALUES ('" + _r103.Value + "', 'linear', 'v1', 'Title', '', '2026-10-30T09:00:00Z', 'queued')", migrate: false);
        await Execute("INSERT INTO intent_link (record, target, rule, confidence, span_hash, synced_at) VALUES ('" + _r103.Value + "', 'feature:a', 'name', 0.5, 'ab', '2026-10-30T09:00:00Z')", migrate: false);
        var store = new SqliteIntentStore(_database);
        var alias = new IntentLink(_r103, NodeKey.From("concept:invoicing"), LinkRule.Alias, 0.8, "cd");

        var replaced = await store.ReplaceRuleLinks(_r103, LinkRule.Alias, [alias], Ct);
        var stampAfterInsert = await Count("SELECT seq FROM stamp_intent");
        await Execute("DELETE FROM intent_link WHERE rule = 'alias'");
        var stampAfterDelete = await Count("SELECT seq FROM stamp_intent");
        var layer = await store.Layer(Ct);

        Assert.True(replaced.IsSuccess);
        Assert.Equal([("feature:a", LinkRule.Name)], layer.Value.Links.Select(l => (l.Target.Value, l.Rule)));
        Assert.True(stampAfterDelete > stampAfterInsert, "deleting a link no longer moves stamp_intent");
        Assert.True(stampAfterInsert > 0, "inserting a link no longer moves stamp_intent");
    }

    private async Task<long> Count(string sql)
    {
        await using var read = (await (await _database.Writable(Ct)).Value.BeginRead(Ct)).Value;
        return (await SqliteQueries.One(read, new SqlStatement(sql), new LongMapper(), Ct)).Value;
    }

    private async Task Execute(string sql, bool migrate = true)
    {
        var sessions = migrate ? (await _database.Writable(Ct)).Value : _database.Sessions;
        await using var session = (await sessions.BeginSession(Ct)).Value;
        Assert.True((await SqliteQueries.Execute(session, new SqlStatement(sql), Ct)).IsSuccess);
        Assert.True((await session.Commit(Ct)).IsSuccess);
    }

    private sealed class LongMapper : IRowMapper<long>
    {
        public long Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => reader.GetInt64(0);
    }
}
