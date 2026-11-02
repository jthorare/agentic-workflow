using System.Net;
using System.Net.Sockets;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Engine P4a acceptance check 1 (engine spec rev 8.1, "Phase 4a"): a start whose picked ports are taken picks new ones, a bounded
/// number of times.
/// Each test starts the real pinned 'temporal' CLI (binaries.lock); without it the test fails and says how to install it, so a
/// missing binary never reads as a pass. The tests are split across three classes so they run in parallel: each starts and stops
/// its own server on its own home (#1546).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalDevServerPortTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 60_000)]
    public async Task A_picked_port_taken_before_the_server_binds_is_retried_with_new_ports()
    {
        var ct = TestContext.Current.CancellationToken;
        using var rival = Occupy();
        var picks = 0;
        var server = new TemporalDevServer(ChildEnvironment.Start, () => Interlocked.Increment(ref picks) == 1 ? PortOf(rival) : FreePort());   // the first pick (the frontend) loses the race

        var running = await server.StartAsync(new TemporalServerStart(PinnedTemporal.Path(), _home.Path), ct);
        try
        {
            Assert.NotEqual(PortOf(rival), running.Frontend.Port);
            Assert.Equal(DevServerAnswer.Ready, (await DevServerProbe.Ask(running.Frontend, TemporalSettings.DefaultNamespace, TimeSpan.FromSeconds(5), ct)).Answer);
            Assert.Contains("picking new ports", await File.ReadAllTextAsync(Path.Combine(_home.Path, "logs", SindriTemporal.LogFile), ct), StringComparison.Ordinal);
        }
        finally
        {
            await server.StopAsync(running, ct);
        }
    }

    [Fact(Timeout = 90_000)]
    public async Task A_start_whose_ports_are_taken_every_time_gives_up_after_the_bounded_attempts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var rival = Occupy();
        var picks = 0;
        var server = new TemporalDevServer(ChildEnvironment.Start, () => Interlocked.Increment(ref picks) % 3 == 1 ? PortOf(rival) : FreePort());   // the frontend pick of every attempt loses

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            server.StartAsync(new TemporalServerStart(PinnedTemporal.Path(), _home.Path), ct));

        Assert.Contains("address already in use", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TemporalDevServer.MaxPortAttempts * 3, picks);
        Assert.Empty(server.Find(_home.Path));
    }

    private static TcpListener Occupy()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static int PortOf(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    private static int FreePort()
    {
        using var listener = Occupy();
        return PortOf(listener);
    }
}
