using System.Net;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Opens the proxy's outbound connection to an address it already validated.</summary>
internal interface IEgressDialer
{
    /// <summary>Connects to <paramref name="address"/>.</summary>
    /// <param name="address">The validated address.</param>
    /// <param name="port">The port.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The stream; the caller disposes it.</returns>
    public ValueTask<Stream> Dial(IPAddress address, int port, CancellationToken cancellationToken);
}
