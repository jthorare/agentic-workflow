using System.Net;
using System.Net.Sockets;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The handler an 'http' node's real requests go through (agent-first PR 9): no automatic redirects (<see cref="GraphHttpClient"/> follows only same-host ones), no proxy, no cookies,
/// and a connect step that refuses an address the name resolved to when it is loopback, unspecified or link-local, so an allowlisted name can't be pointed at the box itself.
/// </summary>
internal static class GraphHttpHandler
{
    internal static SocketsHttpHandler Create() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        ConnectCallback = Connect,
    };

    /// <summary>Whether a request may connect to <paramref name="address"/>.</summary>
    /// <param name="address">A resolved address.</param>
    /// <returns>False for loopback, unspecified and link-local addresses.</returns>
    internal static bool MayConnect(IPAddress address) =>
        !IPAddress.IsLoopback(address) && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any) && !address.IsIPv6LinkLocal && !address.ToString().StartsWith("169.254.", StringComparison.Ordinal);

    private static async ValueTask<Stream> Connect(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
        var allowed = addresses.Where(MayConnect).ToArray();
        if (allowed.Length == 0)
        {
            throw new HttpRequestException($"'{context.DnsEndPoint.Host}' resolves only to loopback or link-local addresses");
        }

        Socket? socket = new(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            socket.NoDelay = true;
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            var stream = new NetworkStream(socket, ownsSocket: true);
            socket = null;   // why: the stream owns the socket now
            return stream;
        }
        finally
        {
            socket?.Dispose();
        }
    }
}
