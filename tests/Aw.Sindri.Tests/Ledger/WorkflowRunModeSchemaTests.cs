using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>P4a (spec 2026-11-12 §3.2): 'workflow_run' holds the run's mode and its test options, so a test row is told from a live one in SQL.</summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowRunModeSchemaTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact]
    public async Task The_workflow_run_table_has_mode_and_test_options_columns()
    {
        var settings = new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml"));
        using var database = new LedgerDatabase(settings, new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await database.Writable(TestContext.Current.CancellationToken)).Value;

        await using var connection = new SqliteConnection($"Data Source={settings.LedgerPath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('workflow_run') WHERE name IN ('mode', 'test_options') ORDER BY name";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        Assert.Equal(["mode", "test_options"], names);
    }
}
