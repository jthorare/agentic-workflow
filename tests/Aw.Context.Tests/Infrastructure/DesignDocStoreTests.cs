using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Library;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>Migration 0031 and what the record store does with a design doc's items and refs and a PR's claims (PR review page spec 2026-11-04 §5.3, §4.4).</summary>
public sealed class DesignDocStoreTests : IDisposable
{
    private const string DocKey = "repo:aw/docs/harbor.md";
    private const string PrKey = "github:pr/acme/widgets/12";
    private const string Body = "## Goals\n- **REQ-1** Book a ferry.\n- **REQ-2** Cancel a booking.\n| **TP-1** Booking works. Verifies REQ-1 |\n";

    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static StoredRecord DocRecord(string version = "v1") =>
        RecordSamples.Record(DocKey, version, RepoDocs.Id) with { Kind = RecordKind.Doc };

    private static RecordWrite DocWrite(ParsedDesignDoc? parsed, string version = "v1") =>
        RecordSamples.Write(DocRecord(version), "words") with { Doc = parsed };

    private static RecordWrite PrWrite(IReadOnlyList<string>? claims, string version = "v1") =>
        RecordSamples.Write(RecordSamples.Record(PrKey, version, RecordSamples.Github) with { Kind = RecordKind.Pr }, "words") with { Claims = claims };

    private Task<Kernel.Result<CommitReport>> Commit(SourceId source, int page, params RecordWrite[] writes) =>
        _world.Store.Commit(RecordSamples.Batch(source, page, writes), Ct).AsTask();

    private Task<Kernel.Result<CommitReport>> Remove(SourceId source, int page, string key) =>
        _world.Store.Commit(RecordSamples.Batch(source, page) with { Removed = [RecordKey.From(key)] }, Ct).AsTask();

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0031_creates_the_four_tables_and_their_indexes()
    {
        await _world.Migrate();

        Assert.Equal("0031", ContextDatabase.Migrations[30].Version.ToString("D4", CultureInfo.InvariantCulture));
        foreach (var table in new[] { "design_doc", "design_doc_item", "design_doc_ref", "design_doc_claim" })
        {
            Assert.Equal(1, _world.Count($"SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'"));
        }

        foreach (var index in new[] { "design_doc_by_scope_id", "design_doc_ref_by_to_ref", "design_doc_claim_by_ref" })
        {
            Assert.Equal(1, _world.Count($"SELECT count(*) FROM sqlite_master WHERE type = 'index' AND name = '{index}'"));
        }

        Assert.Equal(0, _world.Count("SELECT sum(\"unique\") FROM pragma_index_list('design_doc') WHERE origin = 'c'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0031_invalidates_github_pull_requests_and_restarts_that_feed_and_nothing_else()
    {
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(_world.DatabasePath)), new FakeTimeProvider());
        _ = Directory.CreateDirectory(Path.GetDirectoryName(_world.DatabasePath)!);
        _ = await runner.Migrate([.. ContextDatabase.Migrations.Take(30)], Ct);
        foreach (var (key, source, kind) in new[] { ("github:pr/o/r/1", "github", "pr"), ("github:issue/o/r/2", "github", "issue"), (DocKey, "repo-docs", "doc") })
        {
            _world.Sql($"INSERT INTO source_record (key, source, kind, version, text_hash, ruleset, blocked, synced_at, title) VALUES ('{key}', '{source}', '{kind}', 'keep', 'h', 'r', 0, '2026-10-27T00:00:00Z', 't')");
        }

        foreach (var source in new[] { "github", "repo-docs" })
        {
            _world.Sql($"INSERT INTO source_cursor (source, repo, cursor, last_sync, phase) VALUES ('{source}', 'records', 'mark', '2026-10-27T00:00:00Z', 'incremental')");
        }

        _ = await runner.Migrate(ContextDatabase.Migrations, Ct);

        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record WHERE key = 'github:pr/o/r/1' AND version = ''"));
        Assert.Equal(2, _world.Count("SELECT count(*) FROM source_record WHERE version = 'keep' AND key IN ('github:issue/o/r/2', 'repo:aw/docs/harbor.md')"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'github' AND phase = 'recent' AND cursor IS NULL"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_cursor WHERE source = 'repo-docs' AND phase = 'incremental' AND cursor = 'mark'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_docs_header_items_and_refs_land_with_its_record()
    {
        _ = await Commit(RepoDocs.Id, 1, DocWrite(ADoc.Parse("spec", Body)));

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM design_doc WHERE doc_key = '{DocKey}' AND root = 'repo:aw' AND scope = 'repo:aw' AND doc_id = 'harbor-booking' AND kind = 'spec' AND status = 'draft' AND parser_version = {DesignDocParser.Version} AND errors = '[]'"));
        Assert.Equal(4, _world.Count($"SELECT count(*) FROM design_doc_item WHERE doc_key = '{DocKey}'"));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM design_doc_item WHERE doc_key = '{DocKey}' AND item_id = 'REQ-1' AND item_kind = 'requirement' AND parent_id = 'goals'"));
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM design_doc_ref WHERE doc_key = '{DocKey}' AND from_item = 'TP-1' AND to_ref = 'REQ-1' AND kind = 'verifies'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_second_commit_of_the_same_doc_replaces_its_rows()
    {
        _ = await Commit(RepoDocs.Id, 1, DocWrite(ADoc.Parse("spec", Body)));

        _ = await Commit(RepoDocs.Id, 2, DocWrite(ADoc.Parse("spec", "## Goals\n- **REQ-9** Only this.\n"), version: "v2"));

        Assert.Equal(1, _world.Count("SELECT count(*) FROM design_doc"));
        Assert.Equal(["goals", "REQ-9"], Items());
        Assert.Equal(0, _world.Count("SELECT count(*) FROM design_doc_ref"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_doc_that_stops_being_a_design_doc_loses_its_rows()
    {
        _ = await Commit(RepoDocs.Id, 1, DocWrite(ADoc.Parse("spec", Body)));

        _ = await Commit(RepoDocs.Id, 2, DocWrite(parsed: null, version: "v2"));

        Assert.Equal(0, _world.Count("SELECT count(*) FROM design_doc") + _world.Count("SELECT count(*) FROM design_doc_item"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_removed_doc_loses_its_rows_in_the_same_commit_that_sets_removed_at_and_a_returning_doc_gets_them_again()
    {
        _ = await Commit(RepoDocs.Id, 1, DocWrite(ADoc.Parse("spec", Body)));

        _ = await Remove(RepoDocs.Id, 2, DocKey);

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM source_record WHERE key = '{DocKey}' AND removed_at IS NOT NULL"));
        Assert.Equal(0, _world.Count("SELECT (SELECT count(*) FROM design_doc) + (SELECT count(*) FROM design_doc_item) + (SELECT count(*) FROM design_doc_ref)"));

        _ = await Commit(RepoDocs.Id, 3, DocWrite(ADoc.Parse("spec", Body), version: "v3"));
        Assert.Equal(4, _world.Count("SELECT count(*) FROM design_doc_item"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_doc_with_errors_is_stored_with_them_and_without_items_and_a_blocked_doc_with_the_blocked_error()
    {
        var broken = ADoc.Parse("spec", "## Goals\n- **REQ-** Missing number.\n");
        _ = await Commit(RepoDocs.Id, 1, DocWrite(broken));

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM design_doc WHERE doc_key = '{DocKey}' AND errors LIKE '%SND-LIB-%'"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM design_doc_item"));

        var blocked = RecordSamples.Write(DocRecord("v2") with { Blocked = true, Title = DocKey }) with { Doc = DesignDocParser.Blocked(DocKey) };
        _ = await Commit(RepoDocs.Id, 2, blocked);

        Assert.Equal(1, _world.Count($"SELECT count(*) FROM design_doc WHERE doc_key = '{DocKey}' AND errors LIKE '%blocked%' AND doc_id IS NULL"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM design_doc_item"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_pull_requests_claims_land_replace_and_go_with_the_pull_request()
    {
        _ = await Commit(RecordSamples.Github, 1, PrWrite(["harbor#Ph2", "harbor#REQ-1"]));
        Assert.Equal(["harbor#Ph2", "harbor#REQ-1"], Claims());

        _ = await Commit(RecordSamples.Github, 2, PrWrite(["harbor#Ph3"], version: "v2"));
        Assert.Equal(["harbor#Ph3"], Claims());

        _ = await Commit(RecordSamples.Github, 3, PrWrite(claims: null, version: "v3"));
        Assert.Empty(Claims());

        _ = await Commit(RecordSamples.Github, 4, PrWrite(["harbor#Ph2"], version: "v4"));
        _ = await Remove(RecordSamples.Github, 5, PrKey);
        Assert.Empty(Claims());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_claim_written_before_its_doc_exists_is_stored_at_once_and_the_doc_landing_leaves_it_alone()
    {
        _ = await Commit(RecordSamples.Github, 1, PrWrite(["harbor-booking#REQ-1"]));
        Assert.Equal(["harbor-booking#REQ-1"], Claims());

        _ = await Commit(RepoDocs.Id, 1, DocWrite(ADoc.Parse("spec", Body)));

        Assert.Equal(["harbor-booking#REQ-1"], Claims());
    }

    private string[] Items() => Texts($"SELECT item_id FROM design_doc_item WHERE doc_key = '{DocKey}' ORDER BY line_start");

    private string[] Claims() => Texts("SELECT ref FROM design_doc_claim ORDER BY ref");

    private string[] Texts(string sql)
    {
        var found = new List<string>();
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _world.DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            found.Add(reader.GetString(0));
        }

        return [.. found];
    }
}
