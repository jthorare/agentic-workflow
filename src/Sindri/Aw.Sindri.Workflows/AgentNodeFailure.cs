using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The error a 'run_agent' node fails with when its session ends without an accepted report.</summary>
#pragma warning disable MA0182 // why: nothing in the assembly ends a node yet; the watch loop in GraphWorkflow.Agent (it needs the real launch) is its caller
internal static class AgentNodeFailure
{
    /// <summary>The node error: the reason word ('exit', 'lost', 'idle', 'limit' or 'timeout') and the session's detail.</summary>
    /// <param name="ended">How the watch ended.</param>
    /// <param name="timedOut">True when the node's own timeout ran out before the session ended.</param>
    /// <returns>The message the node run carries.</returns>
    public static string Of(AgentSessionState ended, bool timedOut)
    {
        ArgumentNullException.ThrowIfNull(ended);
        var reason = timedOut ? "timeout" : AgentEndReasons.Of(ended.EndedAs);
        return string.IsNullOrEmpty(ended.Detail) ? $"agent session ended: {reason}" : $"agent session ended: {reason} ({ended.Detail})";
    }
}
