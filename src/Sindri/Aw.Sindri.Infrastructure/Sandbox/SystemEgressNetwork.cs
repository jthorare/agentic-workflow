using System.Net;
using System.Net.Sockets;

namespace Aw.Sindri.Infrastructure.Sandbox;

#pragma warning disable MA0182 // until sandbox launcher (SB-5a) wires it
/// <summary>The real resolver and dialer: system DNS and a TCP connect to the validated address.</summary>
internal sealed class SystemEgressNetwork : IEgressResolver, IEgressDialer
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> Resolve(string host, CancellationToken cancellationToken)
    {
        try
        {
            return await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public async ValueTask<Stream> Dial(IPAddress address, int port, CancellationToken cancellationToken)
    {
#pragma warning disable CA2000 // owned by the returned stream, or disposed on failure
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new NetworkStream(socket, ownsSocket: true);
#pragma warning restore CA2000
    }
}
#pragma warning restore MA0182
