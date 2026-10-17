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

    [Fact, Trait("Lane", "unit")]
    public void Ports_probes_no_fallback_when_the_preferred_port_is_free()
    {
        var probed = new List<int>();

        var facts = HostProbes.Ports(3000, 3001, 3010, port => { probed.Add(port); return true; });

        Assert.Equal([3000], probed);
        Assert.Equal(new PortFacts(3000, PreferredFree: true, FirstFreeFallback: null), facts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ports_scans_the_fallbacks_in_order_and_stops_at_the_first_free_one()
    {
        var probed = new List<int>();

        var facts = HostProbes.Ports(3000, 3001, 3010, port => { probed.Add(port); return port == 3003; });

        Assert.Equal([3000, 3001, 3002, 3003], probed);
        Assert.Equal(new PortFacts(3000, PreferredFree: false, FirstFreeFallback: 3003), facts);
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
    public void Ports_on_a_host_without_ipv6_reads_a_free_port_free_and_a_held_port_taken()
    {
        var holder = new TcpListener(IPAddress.Loopback, 0);
        holder.Start();
        var held = ((IPEndPoint)holder.LocalEndpoint).Port;
        var probes = new HostProbes(ipv6Available: false);
        try
        {
            Assert.False(probes.Ports(held, held, held).PreferredFree);
        }
        finally
        {
            holder.Stop();
        }

        Assert.True(probes.Ports(held, held, held).PreferredFree);
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
}
