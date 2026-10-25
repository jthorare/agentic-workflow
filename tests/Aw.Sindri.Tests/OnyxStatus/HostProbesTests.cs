using System.Net;
using System.Net.Sockets;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Tests.OnyxStatus;

public sealed class HostProbesTests
{
    [Fact, Trait("Lane", "integration")]
    public void Ports_reports_a_bound_port_as_taken()
    {
        var holder = new TcpListener(IPAddress.Loopback, 0);
        holder.Start();
        try
        {
            var port = ((IPEndPoint)holder.LocalEndpoint).Port;

            var facts = new HostProbes().Ports(port, port, port);

            Assert.False(facts.PreferredFree);
            Assert.Null(facts.FirstFreeFallback);
        }
        finally
        {
            holder.Stop();
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void Ports_reports_a_port_taken_on_every_interface_as_taken()
    {
        // Docker publishes on 0.0.0.0: a bind to 127.0.0.1 beside it must not count as free.
        var holder = new TcpListener(IPAddress.Any, 0) { ExclusiveAddressUse = false };
        holder.Start();
        try
        {
            var port = ((IPEndPoint)holder.LocalEndpoint).Port;

            Assert.False(new HostProbes().Ports(port, port, port).PreferredFree);
        }
        finally
        {
            holder.Stop();
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_port_in_time_wait_reads_free()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "TIME_WAIT binds differ on Windows, where the probe keeps exclusive use");

        // Another process can take the released port before the probe: try again on a fresh port, and pass when any attempt reads it free.
        Assert.True(await AnyAttemptReadsFree(async () =>
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken);
                using var accepted = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
                accepted.Close();   // the server's end closes first: it is the side left in TIME_WAIT
            }

            listener.Stop();
            return port;
        }, new HostProbes()));
    }

    [Fact, Trait("Lane", "unit")]
    public void Ports_probes_no_fallback_when_the_preferred_port_is_free()
    {
        var probed = new List<int>();

        var facts = HostProbes.Ports(3189, 3190, 3199, port => { probed.Add(port); return true; });

        Assert.Equal([3189], probed);
        Assert.Equal(new PortFacts(3189, PreferredFree: true, FirstFreeFallback: null), facts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ports_scans_the_fallbacks_in_order_and_stops_at_the_first_free_one()
    {
        var probed = new List<int>();

        var facts = HostProbes.Ports(3189, 3190, 3199, port => { probed.Add(port); return port == 3192; });

        Assert.Equal([3189, 3190, 3191, 3192], probed);
        Assert.Equal(new PortFacts(3189, PreferredFree: false, FirstFreeFallback: 3192), facts);
    }

    [Fact, Trait("Lane", "integration")]
    public void Ports_reports_a_port_listening_only_on_ipv6_loopback_as_taken()
    {
        if (!Socket.OSSupportsIPv6)
        {
            return;   // no IPv6 loopback on this box: nothing to prove
        }

        var holder = new TcpListener(IPAddress.IPv6Loopback, 0);
        try
        {
            holder.Start();
        }
        catch (SocketException)
        {
            return;   // IPv6 loopback not usable here
        }

        try
        {
            var port = ((IPEndPoint)holder.LocalEndpoint).Port;

            Assert.False(new HostProbes().Ports(port, port, port).PreferredFree);
        }
        finally
        {
            holder.Stop();
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Ports_on_a_host_without_ipv6_reads_a_free_port_free_and_a_held_port_taken()
    {
        var probes = new HostProbes(ipv6Available: false);

        // The held half is deterministic (the listener stays open across the probe); the free half retries on a fresh port,
        // since another process can take the released one before the probe.
        Assert.True(await AnyAttemptReadsFree(() =>
        {
            var holder = new TcpListener(IPAddress.Loopback, 0);
            holder.Start();
            var held = ((IPEndPoint)holder.LocalEndpoint).Port;
            try
            {
                Assert.False(probes.Ports(held, held, held).PreferredFree);
            }
            finally
            {
                holder.Stop();
            }

            return Task.FromResult(held);
        }, probes));
    }

    [Fact, Trait("Lane", "integration")]
    public void FreeDisk_measures_the_nearest_existing_ancestor()
    {
        var directory = Directory.CreateTempSubdirectory("aw-free-disk-");
        try
        {
            var facts = new HostProbes().FreeDisk(Path.Combine(directory.FullName, "not", "yet", "there"));

            Assert.Equal(directory.FullName, facts.MeasuredPath);
            Assert.True(facts.FreeBytes > 0);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private const int FreeAttempts = 5;

    // Each attempt binds a port, releases it (prepare) and probes it. Nothing keeps a released port free, so a taken answer
    // may be another process winning the port: only every attempt reading taken fails the case.
    private static async Task<bool> AnyAttemptReadsFree(Func<Task<int>> prepare, HostProbes probes)
    {
        for (var attempt = 0; attempt < FreeAttempts; attempt++)
        {
            var port = await prepare();
            if (probes.Ports(port, port, port).PreferredFree)
            {
                return true;
            }
        }

        return false;
    }
}
