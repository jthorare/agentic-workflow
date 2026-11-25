using System.Net;
using System.Net.Sockets;

namespace Aw.Kernel.Testing.Tests;

[Lane(TestLane.Unit)]
public sealed class PortThiefTests
{
    [Fact]
    public void A_port_another_process_holds_reads_as_taken()
    {
        using var other = new TcpListener(IPAddress.Loopback, 0);
        other.Start();
        var port = ((IPEndPoint)other.LocalEndpoint).Port;

        using var thief = PortThief.TryTake(port);

        Assert.Null(thief);
    }

    [Fact]
    public void A_free_port_is_held_until_the_thief_stops()
    {
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        using var thief = PortThief.TryTake(port);
        Assert.SkipWhen(thief is null, "another process took the probed port first");

        using var second = PortThief.TryTake(port);
        Assert.Null(second);
    }
}
