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
        return file is { State: HookState.Asking, Reason: AskingReason.Approval } ? new NodeSessionAsk(ToolName(file.Tool), file.At) : null;
    }

    /// <summary>Long poll: reads the ask every <see cref="AgentNodeAskWatch.ReadEvery"/> and returns when it differs from the one the workflow saw, or after the cap.</summary>
    /// <param name="watch">The launch, the ask seen, the read interval and the cap.</param>
    /// <param name="heartbeat">Called on every read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The ask now; null for none.</returns>
    public async Task<NodeSessionAsk?> Watch(AgentNodeAskWatch watch, Action heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(heartbeat);
        var launch = LaunchId.From(watch.Launch);
        var every = watch.ReadEvery > TimeSpan.Zero ? watch.ReadEvery : TimeSpan.FromSeconds(2);
        var reads = Math.Max(1, (int)Math.Ceiling(watch.Cap / every));   // why: counted in reads, so no clock is read here
        using var timer = new PeriodicTimer(every);
        for (var read = 0; read < reads; read++)
        {
            heartbeat();
            var now = await Asking(launch, cancellationToken).ConfigureAwait(false);
            if (!Equals(now, watch.Seen))
            {
                return now;
            }

            _ = await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }

        return watch.Seen;
    }

    // The hook's own pattern for a tool name: a name that does not match is dropped, so a state file can't smuggle text into an event.
    private static string? ToolName(string? tool) => tool is { Length: > 0 and <= 64 } && tool.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-') ? tool : null;
}
