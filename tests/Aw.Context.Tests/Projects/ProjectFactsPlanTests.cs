using Aw.Context.Infrastructure.Records;
using Aw.Context.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Tests.Projects;

/// <summary>The project read must reach its rows through the two indexes the spec names (projects page spec §6.1), not by scanning the store, however many records it holds.</summary>
public sealed class ProjectFactsPlanTests : IDisposable
{
    private const int Records = 50_000;

    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task ProjectFacts_reads_projects_by_source_and_counts_issues_by_to_key_without_a_scan_at_50000_records()
    {
        await _world.Migrate();
        Seed();

        var plan = Plan();

        Assert.Contains(plan, line => line.StartsWith("SEARCH", StringComparison.Ordinal) && line.Contains("source_record_by_source", StringComparison.Ordinal));
        Assert.Contains(plan, line => line.StartsWith("SEARCH", StringComparison.Ordinal) && line.Contains("record_link_by_to_key", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, line => line.StartsWith("SCAN", StringComparison.Ordinal) && !line.Contains("json_each", StringComparison.Ordinal));
    }

    private void Seed()
    {
        // why: one statement per table, rows from a recursive CTE, so 50,000 records seed in well under a second
        _world.Sql(
            "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < " + Records.ToString(System.Globalization.CultureInfo.InvariantCulture) + ") " +
            "INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at) " +
            "SELECT 'linear:issue/' || i, 'linear', 'issue', NULL, 'linear:SPL', 'Issue ' || i, NULL, NULL, 'open', 'Todo', NULL, '2026-10-27T12:00:00Z', NULL, 'v', 'h', 'r', 0, NULL, '2026-10-27T12:00:00Z' FROM n");
        _world.Sql(
            "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 40) " +
            "INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at) " +
            "SELECT 'linear:project/' || i, 'linear-projects', 'project', NULL, 'linear:SPL', 'Project ' || i, 'https://linear.app/p/' || i, NULL, 'open', 'started', NULL, '2026-10-27T12:00:00Z', NULL, 'v', 'h', 'r', 0, NULL, '2026-10-27T12:00:00Z' FROM n");
        _world.Sql(
            "INSERT INTO record_link (from_key, to_ref, to_key, kind, title) SELECT key, 'linear:project/' || (CAST(substr(key, 14) AS INTEGER) % 40 + 1), 'linear:project/' || (CAST(substr(key, 14) AS INTEGER) % 40 + 1), 'in-project', NULL FROM source_record WHERE kind = 'issue'");
    }

    private List<string> Plan()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _world.DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + RecordSql.ProjectFacts;
        _ = command.Parameters.AddWithValue("@source", "linear-projects");
        _ = command.Parameters.AddWithValue("@audiences", """["linear:SPL"]""");
        _ = command.Parameters.AddWithValue("@records", "[]");
        var lines = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            lines.Add(reader.GetString(3));
        }

        return lines;
    }
}
