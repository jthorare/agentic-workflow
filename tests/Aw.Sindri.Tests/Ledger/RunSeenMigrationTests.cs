using Aw.Kernel.Data;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>Migration 22 ('workflow_run_seen', home spec §3.3, E1) on a file that already holds failed runs: they count as seen, so Home does not fill with old failures.</summary>
[Lane(TestLane.Integration)]
public sealed class RunSeenMigrationTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void The_migration_is_number_22_named_workflow_run_seen_and_reads_start_there()
    {
        Assert.Contains(LedgerDatabase.Migrations, static m => m.Version == 22 && string.Equals(m.Name, "workflow_run_seen", StringComparison.Ordinal));
        Assert.Equal(22, LedgerDatabase.WorkflowRunSeenVersion);
    }

    [Fact]
    public async Task Runs_that_failed_before_the_migration_count_as_seen_and_a_new_failure_does_not()
    {
        var previous = Path.Combine(_home.Path, "previous.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(previous)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate(LedgerDatabase.Migrations, Token, targetVersion: LedgerDatabase.WorkflowRunSeenVersion - 1)).IsSuccess);
        Execute(previous, "INSERT INTO workflow_run (id, workflow_id, revision, trigger, status, outputs, error, item_id, started_at, ended_at, mode) VALUES " +
            "('old-flow@1:1', 'WF-00000000000000000000000000000001', 1, '{\"kind\":\"manual\"}', 'failed', NULL, 'boom', NULL, '2026-11-01T08:00:00.0000000+00:00', '2026-11-01T08:01:00.0000000+00:00', 'live'), " +
            "('old-flow@1:2', 'WF-00000000000000000000000000000001', 1, '{\"kind\":\"schedule\"}', 'failed', NULL, 'boom', NULL, '2026-11-02T08:00:00.0000000+00:00', NULL, 'live')");
        var home = Path.Combine(_home.Path, "home");
        _ = Directory.CreateDirectory(home);
        File.Copy(previous, Path.Combine(home, "ledger.db"));
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        using var database = new LedgerDatabase(new SindriSettings(home, Path.Combine(home, "profile.yaml")), clock);
        Assert.True((await database.Writable(Token)).IsSuccess);
        var seen = new SqliteRunSeenStore(database, clock);

        var afterUpgrade = (await seen.FailedUnseen(10, Token)).Value;
        var fresh = new WorkflowRun(WorkflowRunId.From("old-flow@1:3"), WorkflowId.From("WF-00000000000000000000000000000001"), 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Failed,
            Outputs: null, Error: "boom again", ItemId: null, KnownInstants.Epoch, KnownInstants.Epoch.AddMinutes(1));
        _ = (await new SqliteWorkflowRunStore(database, clock).Insert(fresh, Token)).Value;
        var afterNewFailure = (await seen.FailedUnseen(10, Token)).Value;

        Assert.Empty(afterUpgrade);
        Assert.Equal("old-flow@1:3", Assert.Single(afterNewFailure).Id.Value);
    }

    private static void Execute(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }
}
