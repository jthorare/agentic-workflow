using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Engine P4a acceptance check 1 (engine spec rev 8.1, "Phase 4a"): the dev server starts with 'sindri serve', loopback only,
/// its database under the Sindri home, and stops.
/// Each test starts the real pinned 'temporal' CLI (binaries.lock); without it the test fails and says how to install it, so a
/// missing binary never reads as a pass. The tests are split across three classes so they run in parallel: each starts and stops
/// its own server on its own home (#1546).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalDevServerTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 60_000)]
    public async Task Serve_starts_the_dev_server_on_loopback_with_its_database_under_the_home()
    {
        var temporal = PinnedTemporal.Path();
        var server = new TemporalDevServer(ChildEnvironment.Start);

        var running = await server.StartAsync(new TemporalServerStart(temporal, _home.Path), TestContext.Current.CancellationToken);
        try
        {
            Assert.True(IPAddress.IsLoopback(running.Frontend.Address), $"frontend {running.Frontend} is not loopback");
            Assert.Equal(Path.Combine(_home.Path, SindriTemporal.DatabaseFile), running.DatabasePath);
            Assert.True(File.Exists(running.DatabasePath), $"no database at {running.DatabasePath}");
            Assert.True(running.ProcessId > 0);
        }
        finally
        {
            await server.StopAsync(running, TestContext.Current.CancellationToken);
        }
    }

    [Fact(Timeout = 60_000)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_start_whose_server_comes_up_seconds_late_waits_for_it_and_is_not_cut_short_by_the_read_bound()
    {
        var ct = TestContext.Current.CancellationToken;
        var slow = Path.Combine(_home.Path, "slow-temporal.sh");
        _ = ExecutableScript.Write(slow, $"sleep 4\nexec \"{PinnedTemporal.Path()}\" \"$@\"\n");   // the pinned CLI, 4 s after the process starts: a cold box
        var server = new TemporalDevServer(ChildEnvironment.Start);

        var running = await server.StartAsync(new TemporalServerStart(slow, _home.Path), ct);
        try
        {
            Assert.Equal(DevServerAnswer.Ready, (await DevServerProbe.Ask(running.Frontend, TemporalSettings.DefaultNamespace, TimeSpan.FromSeconds(5), ct)).Answer);
        }
        finally
        {
            await server.StopAsync(running, ct);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task Stop_takes_the_server_down()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new TemporalDevServer(ChildEnvironment.Start);
        var running = await server.StartAsync(new TemporalServerStart(PinnedTemporal.Path(), _home.Path), ct);

        await server.StopAsync(running, ct);

        Assert.True(DevServerProcesses.Gone(running.ProcessId));
        Assert.Empty(server.Find(_home.Path));
    }

    [Fact]
    public void The_pin_in_code_is_the_pin_in_binaries_lock() => Assert.Equal(SindriTemporal.PinnedVersion, PinnedTemporal.Version);
}
