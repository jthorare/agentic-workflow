using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Sends an 'http' node's request (agent-first PR 9): only to a host of 'workflow.http.hosts', never to a loopback address, never with a credential header, without following a
/// redirect to another host, and reading at most 1 MB of the answer. Before connections exist (PR 10) a request is always unauthenticated.
/// </summary>
/// <param name="handler">What sends the request; a fake in tests. It must not follow redirects itself.</param>
/// <param name="hosts">Reads the allowlist each time, so a profile edit applies at once.</param>
internal sealed class GraphHttpClient(HttpMessageHandler handler, Func<CancellationToken, ValueTask<IReadOnlyList<string>>> hosts) : IGraphHttpPort
{
    /// <summary>The most bytes of an answer that are read.</summary>
    internal const int MaxBodyBytes = 1024 * 1024;

    /// <summary>The allowlist of the approved profile; none (so every request is refused) when the profile can't be read.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The hosts.</returns>
    internal static async ValueTask<IReadOnlyList<string>> HostsOf(IApprovedProfile profile, CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsSuccess ? latest.Value?.Profile.Workflow?.Http?.Hosts ?? [] : [];
    }

    public ValueTask<Result<GraphHttpResponse>> Send(GraphHttpRequest request, CancellationToken cancellationToken)
    {
        _ = (handler, hosts);
        throw new NotSupportedException("GraphHttpClient is not written yet");
    }
}
