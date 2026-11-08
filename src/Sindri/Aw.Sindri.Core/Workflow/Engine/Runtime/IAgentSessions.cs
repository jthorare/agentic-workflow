using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What the engine asks about an agent Step's launched session (engine P8 D3, D9): whether it still runs, and what it has spent. Its adapter reads the ledger's 'session.ended' and 'session.usage' facts.</summary>
public interface IAgentSessions
{
    /// <summary>Whether the launch's session is live or has ended. A launch with no 'session.ended' is live.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state; an error when the facts can't be read.</returns>
    public ValueTask<Result<AgentSessionState>> Watch(LaunchId launch, CancellationToken cancellationToken);

    /// <summary>The tokens the session has spent so far, as a Step's budget counts them: fresh input, cache writes and output of its latest usage sample (samples are cumulative), without the cache reads a session re-counts every turn. Zero when there is none.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tokens; an error when the facts can't be read.</returns>
    public ValueTask<Result<long>> Usage(LaunchId launch, CancellationToken cancellationToken);
}
