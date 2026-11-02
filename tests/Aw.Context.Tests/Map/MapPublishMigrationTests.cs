using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>Migration 0010 (the hold, the wait's chain and the versions' heads) over a store written by the build before it (integration, a temp home).</summary>
public sealed class MapPublishMigrationTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly ContextStoreSettings _settings;
    private readonly ContextDatabase _database;

    public MapPublishMigrationTests()
    {
        _settings = SettingsFromEnvironment.Read(_home.Environment);
        _database = new ContextDatabase(_settings, new FakeTimeProvider());
    }

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public void The_publish_tables_are_migration_0010_named_for_the_map_stage() =>
        Assert.Contains(ContextDatabase.Migrations, m => m.Version == 10 && m.Name == "s4p_map_publish");

    [Fact, Trait("Lane", "integration")]
    public async Task An_old_context_db_opens_keeps_its_versions_and_gets_the_three_tables()
    {
        var connections = new SqliteConnectionFactory(new SqliteOptions(_settings.DatabasePath));
        _ = Directory.CreateDirectory(_settings.Home);
        Assert.True((await new MigrationRunner(connections, new FakeTimeProvider()).Migrate(ContextDatabase.Migrations, Ct, targetVersion: 9)).IsSuccess);
        await Execute("INSERT INTO map_version (repo, commit_sha, key_version, created, status) VALUES ('aw-split-panes', 'a41b07d', 1, '2026-07-15T10:00:00Z', 'current')", migrate: false);
        var store = new SqliteGraphStore(_database, _settings);

        var before = await store.Held(Ct);   // a read on an old store never migrates it and finds nothing held

        Assert.Equal((null, null, 1), (before.Value.Hold, before.Value.LatestDraft, before.Value.Current));
        Assert.Empty(before.Value.CurrentHeads);
        var draft = (await store.SaveDraft(SampleMap.Build() with { Heads = [new MapHead(ContextRepoName.From("aw-split-panes"), "a41b07d", OnDefault: true)] }, SampleMap.At, Ct)).Value;   // the first write migrates

        Assert.Equal(3L, await Count("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name IN ('map_hold', 'map_publish_chain', 'map_version_head')"));
        Assert.Equal(1L, await Count("SELECT count(*) FROM map_version WHERE status = 'current' AND id = 1"));
        Assert.True((await store.Hold(new MapHold(draft.Number, SampleMap.At, []), Ct)).IsSuccess);
        Assert.Equal(1L, await Count("SELECT count(*) FROM map_hold"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_tables_cascade_with_their_version_and_the_head_columns_refuse_a_bad_commit_or_a_second_chain()
    {
        var store = new SqliteGraphStore(_database, _settings);
        var draft = (await store.SaveDraft(SampleMap.Build() with { Heads = [new MapHead(ContextRepoName.From("aw-split-panes"), "a41b07d", OnDefault: true)] }, SampleMap.At, Ct)).Value;
        _ = await store.Hold(new MapHold(draft.Number, SampleMap.At, []), Ct);

        Assert.False(await TryExecute(string.Create(CultureInfo.InvariantCulture, $"INSERT INTO map_version_head (version, repo, commit_sha, on_default) VALUES ({draft.Number}, 'x', 'NOT-HEX', 1)")));
        Assert.False(await TryExecute("INSERT INTO map_publish_chain (id, since) VALUES (2, '2026-07-15T10:00:00Z')"));
        Assert.True(await TryExecute(string.Create(CultureInfo.InvariantCulture, $"DELETE FROM map_version WHERE id = {draft.Number}")));

        Assert.Equal(0L, await Count("SELECT count(*) FROM map_version_head"));
        Assert.Equal(0L, await Count("SELECT count(*) FROM map_hold"));
        Assert.Equal(1L, await Count("SELECT count(*) FROM map_publish_chain"));   // the chain is not tied to a draft
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_head_keeps_the_code_index_commit_it_was_built_with_and_a_head_from_before_the_column_reads_as_none()
    {
        var connections = new SqliteConnectionFactory(new SqliteOptions(_settings.DatabasePath));
        _ = Directory.CreateDirectory(_settings.Home);
        Assert.True((await new MigrationRunner(connections, new FakeTimeProvider()).Migrate(ContextDatabase.Migrations, Ct, targetVersion: 24)).IsSuccess);
        await Execute("INSERT INTO map_version (repo, commit_sha, key_version, created, status) VALUES ('aw-split-panes', 'a41b07d', 1, '2026-07-15T10:00:00Z', 'draft')", migrate: false);
        await Execute("INSERT INTO map_version_head (version, repo, commit_sha, on_default) VALUES (1, 'aw-split-panes', 'a41b07d', 1)", migrate: false);
        var store = new SqliteGraphStore(_database, _settings);

        var old = await store.Heads(1, Ct);   // a read on a store before 0025 never migrates it: the head has no index commit

        Assert.Null(Assert.Single(old.Value).IndexCommit);
        var repo = ContextRepoName.From("aw-split-panes");
        var saved = (await store.SaveDraft(SampleMap.Build() with { Heads = [new MapHead(repo, "a41b07d", OnDefault: true) { IndexCommit = "a41b07d" }, new MapHead(ContextRepoName.From("aw-other"), "b52c18e", OnDefault: true)] }, SampleMap.At, Ct)).Value;   // the first write migrates

        var heads = (await store.Heads(saved.Number, Ct)).Value;
        Assert.Equal(("a41b07d", null), (heads[0].IndexCommit, heads[1].IndexCommit));
    }

    private async Task<long> Count(string sql)
    {
        var sessions = (await _database.Writable(Ct)).Value;
        await using var session = (await sessions.BeginSession(Ct)).Value;
        return (await SqliteQueries.One(session, new SqlStatement(sql), new LongMapper(), Ct)).Value;
    }

    private async Task Execute(string sql, bool migrate = true) => Assert.True(await TryExecute(sql, migrate));

    private async Task<bool> TryExecute(string sql, bool migrate = true)
    {
        var sessions = migrate ? (await _database.Writable(Ct)).Value : _database.Sessions;
        await using var session = (await sessions.BeginSession(Ct)).Value;
        if ((await SqliteQueries.Execute(session, new SqlStatement(sql), Ct)).IsError)
        {
            return false;
        }

        return (await session.Commit(Ct)).IsSuccess;
    }

    private sealed class LongMapper : IRowMapper<long>
    {
        public long Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => reader.GetInt64(0);
    }
}
