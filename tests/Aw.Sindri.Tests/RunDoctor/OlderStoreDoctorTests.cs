using System.Globalization;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Views;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.RunDoctor;

/// <summary>
/// 'sindri doctor' on a home an older build wrote (first-run spec §3.4, #2954, #3317): a store behind this build's migrations is one fail row with the restart fix, and a SQLite
/// failure inside a check is a fail row naming the store, never a crash. The other rows still print.
/// </summary>
public sealed class OlderStoreDoctorTests : IDisposable
{
    private const string Restart = "sindri stop && sindri";

    private readonly TempHome _home = new();
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SindriSettings Settings => new(_home.Path, Path.Combine(_home.Path, "profile.yaml"));

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Doctor_prints_a_fail_row_for_a_ledger_db_one_migration_behind()
    {
        var needs = LedgerDatabase.Migrations.Count;
        await MigrateTo(Settings.LedgerPath, LedgerDatabase.Migrations, needs - 1);

        var report = await Doctor();

        var ledger = report.Checks.Single(c => c.Name == "ledger");
        Assert.Equal(new DoctorCheck("ledger", CheckState.Fail, string.Create(CultureInfo.InvariantCulture, $"ledger.db is older than this build (schema {needs - 1}, this build needs {needs})"), Restart), ledger);
        Assert.Contains(report.Checks, c => c.Name == "lease");
        Assert.Contains(report.Checks, c => c.Name == "views");
        Assert.NotEqual(0, report.ExitCode);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Doctor_prints_a_fail_row_for_a_views_db_one_migration_behind()
    {
        var needs = ViewsDatabase.Migrations.Count;
        await MigrateTo(Settings.LedgerPath, LedgerDatabase.Migrations, LedgerDatabase.Migrations.Count);
        await MigrateTo(Settings.ViewsPath, ViewsDatabase.Migrations, needs - 1);   // #2954: an older serve's views.db, without the column this build selects

        var report = await Doctor();

        var views = report.Checks.Single(c => c.Name == "views");
        Assert.Equal(new DoctorCheck("views", CheckState.Fail, string.Create(CultureInfo.InvariantCulture, $"views.db is older than this build (schema {needs - 1}, this build needs {needs})"), Restart), views);
        Assert.Equal(CheckState.Ok, report.Checks.Single(c => c.Name == "ledger").State);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_sqlite_error_in_a_check_is_a_fail_row_naming_the_store()
    {
        using (var connection = new SqliteConnection($"Data Source={Settings.LedgerPath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE junk (x INTEGER)";   // a SQLite file with none of the ledger's tables: every read is 'no such table', which no store maps
            _ = command.ExecuteNonQuery();
        }

        var report = await Doctor();

        var ledger = report.Checks.Single(c => c.Name == "ledger");
        var lease = report.Checks.Single(c => c.Name == "lease");
        Assert.Equal(CheckState.Fail, ledger.State);
        Assert.StartsWith("ledger.db can't be read: ", ledger.Detail, StringComparison.Ordinal);
        Assert.Equal(CheckState.Fail, lease.State);
        Assert.StartsWith("ledger.db can't be read: ", lease.Detail, StringComparison.Ordinal);
        Assert.Contains(report.Checks, c => c.Name == "profile");
        Assert.NotEqual(0, report.ExitCode);
    }

    private static async Task MigrateTo(string path, IReadOnlyList<Migration> migrations, int version)
    {
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        var migrated = await runner.Migrate(migrations, Token, targetVersion: version);
        Assert.True(migrated.IsSuccess, migrated.IsError ? migrated.Error.Message : null);
    }

    private async Task<RunDoctorResponse> Doctor()
    {
        using var ledger = new LedgerDatabase(Settings, _h.Clock);
        using var views = new SqliteViews(_home.Path);
        var store = new SqliteLedger(ledger, new RecordingLedgerSignal());
        var service = new RunDoctorService(new SqliteLease(ledger, _h.Probe, _h.Clock), _h.Probe, new LedgerFacts(store, views.Reader, new SqliteDoctorStores(views.Database)), new ProfileFacts(_h.Profiles,
            new ContextEngineChecks(FakeContextEngine.Instance, new MapPublishDoctorCheck(new MapPublishStatusReader(FakeContextEngine.Instance, new NoIndexOutlooks(), _h.Store, _h.Clock), _h.Clock), new LinearDoctor(FakeContextEngine.Instance, new PointerSecrets(_ => null, new FakeToolRunner(ToolRun.Missing))), new Features.CodeIndexJobs.CodeIndexDoctor(new CodeIndex.Testing.FakeCodeIndex(), new CodeIndex.Testing.FakeRepoHeads(), new InMemoryJobViews(), new Features.CodeIndexJobs.CodeIndexProbes(new CodeIndex.Testing.FakeEmbedder(), new CodeIndex.Testing.FakePins()), _h.Clock, _h.Profiles, new Server.NoPauses()), new FirstMapDoctorCheck(new InMemoryJobViews(), new Server.NoPauses()))),
            new RuntimeFacts(new FakeControlPlaneProbe(), new FakeTemporalServer()), _h.Clock);
        return (await service.Handle(new RunDoctorRequest(), Token)).Value;
    }
}
