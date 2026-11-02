using System.Net;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Resolves a host name for the proxy (the session has no DNS of its own).</summary>
internal interface IEgressResolver
{
    /// <summary>Resolves <paramref name="host"/>.</summary>
    /// <param name="host">The name.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The addresses; empty when the name doesn't resolve.</returns>
    public ValueTask<IReadOnlyList<IPAddress>> Resolve(string host, CancellationToken cancellationToken);
}
