using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'AskingAgentNode' reads (#3151): whether the launch's session waits on a permission prompt. It finds the session through the launch's row and reads that session's aw:fleet state file,
/// which holds the tool only for an approval ask and never the tool's input.
/// </summary>
/// <param name="views">The launches view, which names the launch's session.</param>
/// <param name="fleet">The aw:fleet state files.</param>
internal sealed class AgentNodeAsking(ILaunchViews views, IFleetStates fleet)
{
    /// <summary>Reads the ask once.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tool and when; null when the session is not asking for an approval, has no known session yet, or the files can't be read.</returns>
    public async ValueTask<NodeSessionAsk?> Asking(LaunchId launch, CancellationToken cancellationToken)
    {
        var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (row.IsError || row.Value?.ResumableId is not { } session)
        {
            return null;
        }

        var files = fleet.Read(cancellationToken);
        if (files.IsError)
        {
            return null;
        }

        var file = files.Value.FirstOrDefault(f => f.Provider == row.Value.Target && f.Session == session);
        return file is { State: HookState.Asking, Reason: AskingReason.Approval, Tool: { } tool } ? new NodeSessionAsk(tool, file.At) : null;
    }
}
