using System.Globalization;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Records;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>Migration 0023 (a project's description column and the one-time re-read of Linear projects) and what the record store does with the description.</summary>
public sealed class ProjectDescriptionStoreTests : IDisposable
{
    private static readonly RecordAudience _teamA = new(new HashSet<AudienceKey> { AudienceKey.From("linear:team-a") }, new HashSet<RecordKey>());

    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static StoredRecord Project(string description = "Ship the thing.") =>
        RecordSamples.Record("linear:project/p1", source: LinearSourceIds.Projects) with { Kind = RecordKind.Project, Description = description };

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0023_adds_the_description_column()
    {
        await _world.Migrate();

        Assert.Equal("0023", ContextDatabase.Migrations[22].Version.ToString("D4", CultureInfo.InvariantCulture));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM pragma_table_info('source_record') WHERE name = 'description'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_description_round_trips_through_the_record_and_the_project_row()
    {
        var project = Project();
        _ = await _world.Store.Commit(RecordSamples.Batch(LinearSourceIds.Projects, 1, RecordSamples.Write(project, "words")), TestContext.Current.CancellationToken);

        var read = (await _world.Store.Get(project.Key, TestContext.Current.CancellationToken)).Value!;
        var row = Assert.Single((await _world.Store.ProjectFacts(LinearSourceIds.Projects, _teamA, TestContext.Current.CancellationToken)).Value);

        Assert.Equal("Ship the thing.", read.Description);
        Assert.Equal("Ship the thing.", row.Description);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tombstone_keeps_no_description()
    {
        var project = Project();
        _ = await _world.Store.Commit(RecordSamples.Batch(LinearSourceIds.Projects, 1, RecordSamples.Write(project, "words")), TestContext.Current.CancellationToken);

        _ = await _world.Store.Commit(RecordSamples.Batch(LinearSourceIds.Projects, 2) with { Removed = [project.Key] }, TestContext.Current.CancellationToken);

        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record WHERE key = 'linear:project/p1' AND removed_at IS NOT NULL AND description IS NULL"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_blocked_project_keeps_no_description()
    {
        var project = RecordSamples.Record("linear:project/p1", source: LinearSourceIds.Projects, blocked: true) with { Kind = RecordKind.Project, Description = "leaked" };
        _ = await _world.Store.Commit(RecordSamples.Batch(LinearSourceIds.Projects, 1, RecordSamples.Write(project)), TestContext.Current.CancellationToken);

        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record WHERE key = 'linear:project/p1' AND description IS NULL"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0023_makes_the_next_commit_of_an_unchanged_project_store_its_description_and_leaves_issues_and_other_cursors_alone()
    {
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(_world.DatabasePath)), new FakeTimeProvider());
        _ = Directory.CreateDirectory(Path.GetDirectoryName(_world.DatabasePath)!);
        _ = await runner.Migrate([.. ContextDatabase.Migrations.Take(22)], TestContext.Current.CancellationToken);
        var project = Project();
        _world.Sql($"INSERT INTO source_record (key, source, kind, version, text_hash, ruleset, blocked, synced_at, title) VALUES ('linear:project/p1', 'linear-projects', 'project', '{project.Version.Value}', '{project.TextHash}', '{project.Ruleset}', 0, '2026-10-27T00:00:00Z', 'Old')");
        _world.Sql("INSERT INTO source_record (key, source, kind, version, text_hash, ruleset, blocked, synced_at, title) VALUES ('linear:issue/1', 'linear', 'issue', 'keep', 'h', 'r', 0, '2026-10-27T00:00:00Z', 'Issue')");
        _world.Sql("INSERT INTO source_cursor (source, repo, cursor, last_sync, phase) VALUES ('linear-projects', 'records', 'watermark', '2026-10-27T00:00:00Z', 'incremental')");
        _world.Sql("INSERT INTO source_cursor (source, repo, cursor, last_sync, phase) VALUES ('linear', 'records', 'issues-mark', '2026-10-27T00:00:00Z', 'incremental')");

        _ = await runner.Migrate(ContextDatabase.Migrations, TestContext.Current.CancellationToken);

        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'linear-projects' AND repo = 'records' AND phase = 'recent' AND cursor IS NULL AND backfill_cursor IS NULL AND backfill_before IS NULL"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'linear' AND repo = 'records' AND phase = 'incremental' AND cursor = 'issues-mark'"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record WHERE key = 'linear:issue/1' AND version = 'keep'"));

        // The same version and text as before the migration: without the invalidation the store would skip this write.
        var commit = await _world.Store.Commit(RecordSamples.Batch(LinearSourceIds.Projects, 1, RecordSamples.Write(project, "words")), TestContext.Current.CancellationToken);

        Assert.Equal(1, commit.Value.Written);
        Assert.Equal("Ship the thing.", (await _world.Store.Get(project.Key, TestContext.Current.CancellationToken)).Value!.Description);
    }
}
