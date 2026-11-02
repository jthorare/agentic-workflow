using System.Globalization;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>Migration 0029: the one-time re-read that stores the PR links (projects page spec §4.5) on Linear issues and GitHub PRs saved before them.</summary>
public sealed class PrLinkRereadMigrationTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0029_invalidates_linear_issues_and_github_prs_and_restarts_both_feeds_and_nothing_else()
    {
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(_world.DatabasePath)), new FakeTimeProvider());
        _ = Directory.CreateDirectory(Path.GetDirectoryName(_world.DatabasePath) ?? ".");
        _ = await runner.Migrate([.. ContextDatabase.Migrations.Take(28)], TestContext.Current.CancellationToken);
        foreach (var (key, source, kind) in new[] { ("linear:issue/1", "linear", "issue"), ("github:pr/o/r/1", "github", "pr"), ("github:issue/o/r/2", "github", "issue"), ("linear:project/p", "linear-projects", "project") })
        {
            _world.Sql($"INSERT INTO source_record (key, source, kind, version, text_hash, ruleset, blocked, synced_at, title) VALUES ('{key}', '{source}', '{kind}', 'keep', 'h', 'r', 0, '2026-10-27T00:00:00Z', 't')");
        }

        foreach (var source in new[] { "linear", "github", "linear-projects" })
        {
            _world.Sql($"INSERT INTO source_cursor (source, repo, cursor, last_sync, phase, backfill_cursor) VALUES ('{source}', 'records', 'mark', '2026-10-27T00:00:00Z', 'incremental', 'b')");
        }

        _ = await runner.Migrate(ContextDatabase.Migrations, TestContext.Current.CancellationToken);

        Assert.Equal("0029", ContextDatabase.Migrations[28].Version.ToString("D4", CultureInfo.InvariantCulture));
        Assert.Equal(2, _world.Count("SELECT count(*) FROM source_record WHERE version = '' AND key IN ('linear:issue/1', 'github:pr/o/r/1')"));
        Assert.Equal(2, _world.Count("SELECT count(*) FROM source_record WHERE version = 'keep' AND key IN ('github:issue/o/r/2', 'linear:project/p')"));
        Assert.Equal(2, _world.Count("SELECT count(*) FROM source_cursor WHERE source IN ('linear', 'github') AND phase = 'recent' AND cursor IS NULL AND backfill_cursor IS NULL AND backfill_before IS NULL"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'linear-projects' AND phase = 'incremental' AND cursor = 'mark'"));
    }
}
