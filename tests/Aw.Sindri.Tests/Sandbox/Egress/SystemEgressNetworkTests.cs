using System.Net;
using System.Net.Sockets;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class SystemEgressNetworkTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task Dial_connects_to_the_address_it_is_given()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken).AsTask();

        await using var stream = await new SystemEgressNetwork().Dial(IPAddress.Loopback, port, TestContext.Current.CancellationToken);
        await stream.WriteAsync("x"u8.ToArray(), TestContext.Current.CancellationToken);

        using var server = await accepted;
        var got = new byte[1];
        await server.GetStream().ReadExactlyAsync(got, TestContext.Current.CancellationToken);
        Assert.Equal((byte)'x', got[0]);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Dial_to_a_closed_port_throws_a_socket_exception()
    {
        var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        var port = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();

        _ = await Assert.ThrowsAsync<SocketException>(async () => await new SystemEgressNetwork().Dial(IPAddress.Loopback, port, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Resolve_finds_localhost_and_returns_nothing_for_a_name_that_does_not_exist()
    {
        var network = new SystemEgressNetwork();

        Assert.Contains(await network.Resolve("localhost", TestContext.Current.CancellationToken), IPAddress.IsLoopback);
        Assert.Empty(await network.Resolve("no-such-host.invalid", TestContext.Current.CancellationToken));
    }
}
