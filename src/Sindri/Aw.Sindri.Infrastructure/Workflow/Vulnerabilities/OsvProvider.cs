#pragma warning disable MA0182 // why: red stub; the green commit registers it
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.Vulnerabilities;

/// <summary>OSV.dev's batch API (no key): one batch query, then one read per advisory found.</summary>
/// <param name="http">Sends the requests.</param>
/// <param name="allowedHosts">The hosts it may call.</param>
internal sealed class OsvProvider(HttpClient http, IReadOnlySet<string> allowedHosts) : IVulnerabilityProvider
{
    /// <inheritdoc />
    public string Name => "osv";

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<Vulnerability>>> Query(IReadOnlyList<VulnPackage> packages, CancellationToken cancellationToken) =>
        throw new NotSupportedException(http.GetType().Name + allowedHosts.GetType().Name);
}
