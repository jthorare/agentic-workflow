using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>Migration 0028 adds map_version.pin_hashes (integration, a temp home): a map built before it reads NULL, never an empty set.</summary>
public sealed class MapPinHashesMigrationTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TempHome _home = new();
    private readonly ContextStoreSettings _settings;
    private readonly ContextDatabase _database;

    public MapPinHashesMigrationTests()
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
    public async Task A_map_made_before_0028_reads_no_pin_hashes_and_a_later_one_keeps_its_own()
    {
        var connections = new SqliteConnectionFactory(new SqliteOptions(_settings.DatabasePath));
        Directory.CreateDirectory(_settings.Home);
        Assert.True((await new MigrationRunner(connections, new FakeTimeProvider()).Migrate(ContextDatabase.Migrations, Ct, targetVersion: 27)).IsSuccess);
        await using (var session = (await _database.Sessions.BeginSession(Ct)).Value)
        {
            Assert.True((await SqliteQueries.Execute(session, new SqlStatement("INSERT INTO map_version (repo, commit_sha, key_version, created, status, ruleset) VALUES ('aw-split-panes', NULL, 1, '2026-10-30T09:00:00Z', 'current', 'old-rules')"), Ct)).IsSuccess);
            Assert.True((await session.Commit(Ct)).IsSuccess);
        }

        var store = new SqliteGraphStore(_database, _settings);
        var before = await store.Held(Ct);
        _ = await store.SaveCurrent(SampleMap.Build() with { PinHashes = new Dictionary<string, string>(StringComparer.Ordinal) { ["billing"] = "aa" } }, SampleMap.At, Ct);
        var after = await store.Held(Ct);

        Assert.True(before.IsSuccess, before.IsError ? before.Error.Message : null);
        Assert.Null(before.Value.CurrentPinHashes);
        Assert.Equal("old-rules", before.Value.CurrentRuleset);
        Assert.Equal("aa", after.Value.CurrentPinHashes!["billing"]);
    }
}
