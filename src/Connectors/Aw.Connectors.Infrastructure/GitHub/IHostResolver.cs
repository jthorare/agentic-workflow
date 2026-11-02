using System.Net;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>Resolves a host name to its addresses; the seam the host rules (and their tests) use instead of DNS.</summary>
internal interface IHostResolver
{
    /// <summary>Resolves <paramref name="host"/>.</summary>
    /// <param name="host">A DNS name (never an IP literal).</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>Every address the name has; a failed lookup throws <see cref="System.Net.Sockets.SocketException"/>.</returns>
    public ValueTask<IReadOnlyList<IPAddress>> Resolve(string host, CancellationToken cancellationToken);
}
