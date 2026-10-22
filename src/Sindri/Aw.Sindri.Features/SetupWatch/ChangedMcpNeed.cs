using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>Lets the automatic registration ask again only when something changed since it last looked at a repo: a new approved profile hash, this process's first tick, or a different port (setup-once spec §5.4, SO-A4).</summary>
/// <param name="inner">Says whether a registration would change anything.</param>
/// <param name="approvals">The approved profile snapshot.</param>
/// <param name="endpoint">The running control plane's port.</param>
/// <param name="memory">What this process has already looked at.</param>
internal sealed class ChangedMcpNeed(IMcpNeed inner, IApprovedProfile approvals, IEndpointFile endpoint, McpAutoMemory memory) : IMcpNeed
{
    public async ValueTask<bool> Needed(RepoName repo, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved is not { IsSuccess: true, Value: { } profile })
        {
            return false;
        }

        var running = await endpoint.Read(cancellationToken).ConfigureAwait(false);
        var port = McpRegistration.Port(running.IsSuccess ? running.Value?.Url : null, profile.Profile.Api?.Port);
        if (memory.Seen(repo, profile.Hash, port))
        {
            return false;
        }

        var needed = await inner.Needed(repo, cancellationToken).ConfigureAwait(false);
        memory.Remember(repo, profile.Hash, port);
        return needed;
    }
}
