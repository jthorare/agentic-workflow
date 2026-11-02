using System.Net;
using System.Net.Sockets;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class EgressRelayTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task HalfClose_uses_the_seam_on_a_half_closeable_stream()
    {
        var (a, b) = JoinedStream.Pair();

        EgressRelay.HalfClose(a);

        Assert.Equal(0, await b.ReadAsync(new byte[1], TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task HalfClose_of_a_socket_that_is_already_closed_does_not_throw()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, TestContext.Current.CancellationToken);
        var stream = client.GetStream();
        client.Dispose();

        EgressRelay.HalfClose(stream);
    }

    [Fact, Trait("Lane", "unit")]
    public void HalfClose_of_a_plain_stream_does_nothing() => EgressRelay.HalfClose(new MemoryStream());
}
