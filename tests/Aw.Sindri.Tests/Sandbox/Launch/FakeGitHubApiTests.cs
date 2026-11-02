using System.Net;
using System.Net.Sockets;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>#1582: the fake picks a free port and then starts on it, and a parallel test can take the port in between; it starts on another one.</summary>
[Lane(TestLane.Integration)]
public sealed class FakeGitHubApiTests
{
    [Fact]
    public void It_starts_on_another_port_when_the_first_is_taken_before_it_listens()
    {
        using var taker = new TcpListener(IPAddress.Loopback, 0);
        taker.Start();
        var taken = ((IPEndPoint)taker.LocalEndpoint).Port;
        var picks = 0;

        using var fake = new FakeGitHubApi(() => picks++ == 0 ? taken : FreePort());

        Assert.DoesNotContain(":" + taken.ToString(System.Globalization.CultureInfo.InvariantCulture), fake.Url, StringComparison.Ordinal);
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
