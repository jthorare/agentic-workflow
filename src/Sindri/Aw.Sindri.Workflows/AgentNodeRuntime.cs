using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The real <see cref="IAgentNodeRuntime"/> (PR 7): the launch, the session watch, the recovery and the close, each over the launcher and the agent runtime.</summary>
/// <param name="launching">Launches.</param>
/// <param name="watching">Watches.</param>
/// <param name="recovering">Reads the output folder.</param>
/// <param name="closing">Closes.</param>
/// <param name="asking">Reads whether the session waits on a permission prompt.</param>
/// <param name="files">The per-launch files; the held marker lives there.</param>
/// <param name="denials">The denial counters; a node that ends with no report names what the sandbox refused (#3565).</param>
internal sealed class AgentNodeRuntime(AgentNodeLaunching launching, AgentSessionWatch watching, AgentNodeRecovering recovering, AgentNodeClosing closing, AgentNodeAsking asking, ILaunchFiles files, IDenialFiles denials) : IAgentNodeRuntime
{
    public Task<AgentNodeLaunched> Launch(AgentNodeRef node, CancellationToken cancellationToken) => launching.Launch(node, cancellationToken);

    public Task<AgentSessionState> Watch(LaunchId launch, Action heartbeat, CancellationToken cancellationToken) => watching.Watch(launch, heartbeat, cancellationToken);

    public async Task<AgentNodeRecovered> Recover(AgentNodeRecover recover, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recover);
        var recovered = await recovering.Recover(recover, cancellationToken).ConfigureAwait(false);
        if (recovered.Outputs is not null)
        {
            return recovered;
        }

        var launch = NodeRunAddress.Of(recover.Node.Run, recover.Node.Node, recover.Node.ItemIndex).Launch();
        return recovered with { Denials = DenialSummary.Line(await denials.List(launch, cancellationToken).ConfigureAwait(false)) };
    }

    public Task<AgentNodeClosed> Close(AgentCloseRun run, CancellationToken cancellationToken) => closing.Close(run, cancellationToken);

    public Task<NodeSessionAsk?> WatchAsking(AgentNodeAskWatch watch, Action heartbeat, CancellationToken cancellationToken) => asking.Watch(watch, heartbeat, cancellationToken);

    public Task Hold(AgentNodeHeld held, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);
        files.MarkTakenOver(LaunchId.From(held.Launch), held.Held);
        return Task.CompletedTask;
    }
}
