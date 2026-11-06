using System.Globalization;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Records;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>Migration 0032 (the Linear hub's side tables, the lookup tables and the Linear cursor reset; spec 2026-11-04 §4.1, §4.2).</summary>
public sealed class LinearHubMigrationTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0032_creates_the_side_tables_the_lookup_tables_and_their_indexes()
    {
        await _world.Migrate();

        Assert.Equal("0032", ContextDatabase.Migrations[31].Version.ToString("D4", CultureInfo.InvariantCulture));
        foreach (var table in new[] { "linear_issue", "linear_comment", "linear_issue_change", "linear_team", "linear_state", "linear_cycle", "linear_member", "linear_label" })
        {
            Assert.Equal(1, _world.Count($"SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'"));
        }

        foreach (var index in new[] { "linear_issue_by_assignee_state", "linear_issue_by_project", "linear_issue_by_cycle" })
        {
            Assert.Equal(1, _world.Count($"SELECT count(*) FROM sqlite_master WHERE type = 'index' AND name = '{index}'"));
        }

        Assert.Equal(14, _world.Count("SELECT count(*) FROM pragma_table_info('linear_issue')"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0032_resets_the_linear_record_cursor_to_recent_and_leaves_other_sources_alone()
    {
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(_world.DatabasePath)), new FakeTimeProvider());
        _ = Directory.CreateDirectory(Path.GetDirectoryName(_world.DatabasePath)!);
        var at0031 = await runner.Migrate([.. ContextDatabase.Migrations.Take(31)], TestContext.Current.CancellationToken);
        Assert.Equal(31, at0031.Value.ToVersion);
        _world.Sql("INSERT INTO source_cursor (source, repo, cursor, last_sync, phase, backfill_cursor, backfill_before) VALUES ('linear', 'records', 'watermark-1', '2026-11-04T00:00:00Z', 'incremental', 'end-cursor', '2026-10-01T00:00:00Z')");
        _world.Sql("INSERT INTO source_cursor (source, repo, cursor, last_sync, phase, backfill_cursor, backfill_before) VALUES ('github', 'records', 'gh-1', '2026-11-04T00:00:00Z', 'incremental', NULL, NULL)");

        var all = await runner.Migrate([.. ContextDatabase.Migrations.Take(32)], TestContext.Current.CancellationToken);

        Assert.Equal(32, all.Value.ToVersion);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'linear' AND repo = 'records' AND phase = 'recent' AND cursor IS NULL AND backfill_cursor IS NULL AND backfill_before IS NULL"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'github' AND repo = 'records' AND phase = 'incremental' AND cursor = 'gh-1'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_purge_of_the_linear_source_cascades_to_the_issue_comment_and_change_rows()
    {
        await _world.Migrate();
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record("linear:issue/i1"), "words")), TestContext.Current.CancellationToken);
        _world.Sql("INSERT INTO linear_issue (key, identifier, priority, updated_at) VALUES ('linear:issue/i1', 'ENG-1', 0, '2026-11-04T00:00:00Z')");
        _world.Sql("INSERT INTO linear_comment (id, issue_key, created_at, body) VALUES ('c1', 'linear:issue/i1', '2026-11-04T00:00:00Z', 'hi')");
        _world.Sql("INSERT INTO linear_issue_change (issue_key, field, old, new, at, via) VALUES ('linear:issue/i1', 'state', 's1', 's2', '2026-11-04T00:00:00Z', 'feed')");

        _ = await _world.Store.Purge(RecordSamples.Linear, TestContext.Current.CancellationToken);

        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_comment"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM linear_issue_change"));
    }
}
