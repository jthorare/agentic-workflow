using System.Net;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>The system's DNS.</summary>
internal sealed class SystemHostResolver : IHostResolver
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> Resolve(string host, CancellationToken cancellationToken)
        => await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
}
