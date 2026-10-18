using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>What one launched session has used so far, read through the usage reader the agent's catalog entry names.</summary>
public interface ILaunchUsage
{
    /// <summary>The session's tokens, per model.</summary>
    /// <param name="target">The agent the session runs.</param>
    /// <param name="session">The agent's session id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, or null when the agent has no usage reader.</returns>
    public ValueTask<IReadOnlyList<ModelTokens>?> OfSession(AgentId target, Ids.SessionId session, CancellationToken cancellationToken);
}
