using System.Net;
using System.Net.Sockets;
using Aw.Connectors.Core;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>Builds the handler the GitHub client runs on.</summary>
internal static class GitHubHttp
{
    /// <summary>
    /// A handler that follows no redirect and checks the addresses of the host against the host rules when it connects, then connects to those
    /// addresses (not to a second lookup), so a name that answers differently between the check and the connection can't reach a private address.
    /// </summary>
    /// <param name="settings">The settings (the private-host flag).</param>
    /// <param name="resolver">Resolves the host name.</param>
    /// <returns>The handler.</returns>
    public static HttpMessageHandler CreateHandler(GitHubClientSettings settings, IHostResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(resolver);
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var host = context.DnsEndPoint.Host;
                var addresses = await Addresses(host, resolver, cancellationToken).ConfigureAwait(false);
                if (!settings.AllowPrivateHost && (addresses.Count == 0 || !addresses.All(HostPolicy.IsPublic)))
                {
                    throw new HttpRequestException(ConnectorsErrors.HostRefused.Title);
                }

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync([.. addresses], context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
    }

    private static async ValueTask<IReadOnlyList<IPAddress>> Addresses(string host, IHostResolver resolver, CancellationToken cancellationToken)
        => IPAddress.TryParse(host, out var literal) ? [literal] : await resolver.Resolve(host, cancellationToken).ConfigureAwait(false);
}
