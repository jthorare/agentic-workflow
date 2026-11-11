using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Resources;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
[Lane(TestLane.Integration)]
public sealed class ResourceDoctorChecksTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly SindriSettings _settings;
    private readonly LedgerDatabase _ledger;
    private readonly ResourceProvisioner _provisioner;

    public ResourceDoctorChecksTests()
    {
        _settings = new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml"));
        _ledger = new LedgerDatabase(_settings, _clock);
        _provisioner = new ResourceProvisioner(new ResourceStore(Root), _ledger, _clock);
    }

    public void Dispose()
    {
        _ledger.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_home.Path, "resources");

    [Fact]
    public async Task A_failed_bookkeeping_read_gives_a_warn_row_with_the_error()
    {
        // Ensure migrates ledger.db to the resources version; overwriting it with bytes that aren't a database then makes the read
        // fail as Damaged (SQLITE_NOTADB). A dropped table would not do: SQLITE_ERROR is unmapped on purpose and throws.
        var owner = ResourceOwner.Parse("workflow:triage").Value;
        var spec = new ResourceSpec([new DatabaseResource("state")], [], Sandbox: null, []);
        Assert.True((await _provisioner.Ensure(owner, spec, Token)).IsSuccess);
        await BreakLedger();
        var checks = new ResourceDoctorChecks(new ResourceFiles(Root), _provisioner);

        var rows = await checks.Run(Token);

        var row = Assert.Single(rows);
        Assert.Equal(("resources", CheckState.Warn), (row.Name, row.State));
        Assert.StartsWith("AW-DATA-008: ", row.Detail, StringComparison.Ordinal);
        Assert.Equal("check that ledger.db under the Sindri home is readable, then run sindri doctor again", row.Fix);
    }

    private async Task BreakLedger()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            File.Delete(_settings.LedgerPath + suffix);
        }

        await File.WriteAllTextAsync(_settings.LedgerPath, string.Concat(Enumerable.Repeat("this is not a SQLite database\n", 200)), Token);
    }
}
