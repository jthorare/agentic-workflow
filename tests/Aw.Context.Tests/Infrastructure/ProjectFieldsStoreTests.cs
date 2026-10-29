using System.Globalization;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Records;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>Migration 0020 (project fields, the owner's identity, the Linear cursor reset) and what the record store does with the new fields.</summary>
public sealed class ProjectFieldsStoreTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0020_resets_the_linear_record_cursor_to_recent_and_leaves_other_sources_alone()
    {
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(_world.DatabasePath)), new FakeTimeProvider());
        _ = Directory.CreateDirectory(Path.GetDirectoryName(_world.DatabasePath)!);
        var at0019 = await runner.Migrate([.. ContextDatabase.Migrations.Take(19)], TestContext.Current.CancellationToken);
        Assert.Equal(19, at0019.Value.ToVersion);
        _world.Sql("INSERT INTO source_cursor (source, repo, cursor, last_sync, phase, backfill_cursor, backfill_before) VALUES ('linear', 'records', 'watermark-1', '2026-10-27T00:00:00Z', 'incremental', 'end-cursor', '2026-10-01T00:00:00Z')");
        _world.Sql("INSERT INTO source_cursor (source, repo, cursor, last_sync, phase, backfill_cursor, backfill_before) VALUES ('github', 'records', 'gh-1', '2026-10-27T00:00:00Z', 'incremental', NULL, NULL)");

        var all = await runner.Migrate(ContextDatabase.Migrations, TestContext.Current.CancellationToken);

        Assert.Equal(ContextDatabase.Migrations.Count, all.Value.ToVersion);
        Assert.Equal("0020", ContextDatabase.Migrations[19].Version.ToString("D4", CultureInfo.InvariantCulture));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'linear' AND repo = 'records' AND phase = 'recent' AND cursor IS NULL AND backfill_cursor IS NULL AND backfill_before IS NULL"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'github' AND repo = 'records' AND phase = 'incremental' AND cursor = 'gh-1'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0020_adds_the_project_columns_and_the_identity_table()
    {
        await _world.Migrate();

        Assert.Equal(2, _world.Count("SELECT count(*) FROM pragma_table_info('source_record') WHERE name IN ('health', 'due_at')"));
        Assert.Equal(4, _world.Count("SELECT count(*) FROM pragma_table_info('source_identity')"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Health_and_due_date_round_trip_and_a_record_with_neither_reads_back_null()
    {
        var project = RecordSamples.Record("linear:project/p1") with { Kind = RecordKind.Project, Health = "atRisk", DueAt = new DateOnly(2026, 12, 31) };
        var plain = RecordSamples.Record("linear:issue/1");
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(project, "words"), RecordSamples.Write(plain, "words")), TestContext.Current.CancellationToken);

        var read = (await _world.Store.Get(project.Key, TestContext.Current.CancellationToken)).Value!;
        var listed = (await _world.Store.List(new RecordFilter(), after: null, 10, TestContext.Current.CancellationToken)).Value;
        var none = (await _world.Store.Get(plain.Key, TestContext.Current.CancellationToken)).Value!;

        Assert.Equal("atRisk", read.Health);
        Assert.Equal(new DateOnly(2026, 12, 31), read.DueAt);
        Assert.Equal(new DateOnly(2026, 12, 31), listed.Single(r => r.Key == project.Key).DueAt);
        Assert.Null(none.Health);
        Assert.Null(none.DueAt);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_led_by_link_is_stored_and_read_back_as_led_by()
    {
        var project = RecordSamples.Record("linear:project/p1") with { Kind = RecordKind.Project, Links = [new RecordLink("linear:user/u1", RecordLinkKind.LedBy, "Joi")] };
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(project, "words")), TestContext.Current.CancellationToken);

        var link = (await _world.Store.Get(project.Key, TestContext.Current.CancellationToken)).Value!.Links.Single();

        Assert.Equal(RecordLinkKind.LedBy, link.Kind);
        Assert.Equal("Joi", link.Title);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM record_link WHERE kind = 'led-by'"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_led_by_kind_has_the_wire_word_led_by() =>
        Assert.Equal("\"led-by\"", System.Text.Json.JsonSerializer.Serialize(RecordLinkKind.LedBy));

    [Fact, Trait("Lane", "integration")]
    public async Task A_source_identity_is_stored_once_per_source()
    {
        var identities = new Aw.Context.Infrastructure.Records.SqliteSourceIdentityStore(_world.Database);
        _ = await identities.Write(new SourceIdentity(RecordSamples.Linear, "linear:user/u1", "Joi", RecordSamples.Now), TestContext.Current.CancellationToken);
        _ = await identities.Write(new SourceIdentity(RecordSamples.Linear, "linear:user/u2", "Thor", RecordSamples.Now.AddHours(1)), TestContext.Current.CancellationToken);

        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_identity"));
        var read = (await identities.Read(RecordSamples.Linear, TestContext.Current.CancellationToken)).Value;
        Assert.Equal("Thor", read!.Name);
        Assert.Equal(SourceId.From("linear"), read.Source);
    }
}
