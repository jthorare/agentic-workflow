using System.Net;
using System.Net.Sockets;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The Sindri forward: a unix socket relayed to Sindri's loopback port, with no allowlist (it reaches one fixed port).</summary>
/// <param name="sindriPort">Sindri's loopback port.</param>
internal sealed class EgressForward(int sindriPort)
{
    /// <summary>Relays one connection to Sindri.</summary>
    /// <param name="client">The sandbox side.</param>
    /// <param name="cancellationToken">Ends the relay.</param>
    /// <returns>A task that ends with the connection.</returns>
    public async Task Handle(Stream client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, sindriPort), cancellationToken).ConfigureAwait(false);
        var upstream = new NetworkStream(socket, ownsSocket: false);
        await using var _ = upstream.ConfigureAwait(false);
        await EgressRelay.Run(client, upstream, static _ => true, EgressRelay.DefaultHalfCloseGrace, cancellationToken).ConfigureAwait(false);
    }
}
