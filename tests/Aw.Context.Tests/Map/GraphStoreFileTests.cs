using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

public sealed class GraphStoreFileTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task The_store_and_its_wal_and_shm_are_0600_in_a_0700_folder()
    {
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        using var database = new ContextDatabase(settings, new FakeTimeProvider());

        _ = await new SqliteGraphStore(database, settings).SaveCurrent(SampleMap.Build(), SampleMap.At, TestContext.Current.CancellationToken);

        Assert.Equal(settings.DatabasePath, Path.Combine(_home.Root, "context", "context.db"));
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(settings.Home));
        foreach (var file in new[] { settings.DatabasePath, settings.DatabasePath + "-wal", settings.DatabasePath + "-shm" }.Where(File.Exists))
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_read_never_creates_the_store()
    {
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        using var database = new ContextDatabase(settings, new FakeTimeProvider());

        var current = await new SqliteGraphStore(database, settings).Current(TestContext.Current.CancellationToken);

        Assert.Null(current.Value);
        Assert.False(File.Exists(settings.DatabasePath));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_map_review_columns_are_migration_0004_after_the_scrub_ruleset_one_and_the_proposal_runs_table_0005_and_the_intent_links_0006_and_their_creation_time_0007_and_the_map_publish_tables_0010_and_the_name_link_rule_0011_and_the_map_ruleset_0012_and_the_dropped_role_counts_0013_and_the_concept_hints_0014_and_the_scope_runs_0015() =>
        Assert.Equal("1 s1a_repo_docs, 2 s3a_graph, 3 s2w_scrub_ruleset, 4 s4_map_review, 5 s4_proposal_runs, 6 s5b_intent_links, 7 s5b_intent_created, 8 s5c_change_stamp, 9 s5c_intent_stamp, 10 s4p_map_publish, 11 s5e_intent_link_name, 12 s5f_map_ruleset, 13 s6_map_dropped_roles, 14 s5g_concept_hint, 15 s6_scope_runs", string.Join(", ", ContextDatabase.Migrations.Select(m => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{m.Version} {m.Name}"))));

    [Fact, Trait("Lane", "integration")]
    public async Task The_fixture_map_round_trips_through_yaml_the_store_and_back()
    {
        var path = FixtureMap.WriteTo(_home.Root);
        var file = await new MapFileReader(FixtureMap.Json).Read(path, TestContext.Current.CancellationToken);
        var map = Maps.Checked(file.Value.Document);
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        using var database = new ContextDatabase(settings, new FakeTimeProvider());
        var store = new SqliteGraphStore(database, settings);

        var saved = await store.SaveCurrent(map, SampleMap.At, TestContext.Current.CancellationToken);
        var read = await store.Read(saved.Value.Number, TestContext.Current.CancellationToken);

        Assert.Equal(Canonical.Of(map.Graph), Canonical.Of(read.Value));
    }
}
