using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The reason a Step fails with when its agent's session ended without a report (the word the failed Step, the ledger and the app carry).</summary>
internal static class AgentEndReasons
{
    /// <summary>The reason for how the session ended.</summary>
    /// <param name="ended">How it ended; null reads as an exit.</param>
    /// <returns>'lost', 'idle', 'limit' or 'exit'.</returns>
    internal static string Of(AgentSessionEnd? ended) => ended switch
    {
        null or AgentSessionEnd.Exit => "exit",
        AgentSessionEnd.Lost => "lost",
        AgentSessionEnd.Idle => "idle",
        AgentSessionEnd.Limit => "limit",
        _ => throw new ArgumentOutOfRangeException(nameof(ended), ended, "not a way a session ends"),
    };
}
