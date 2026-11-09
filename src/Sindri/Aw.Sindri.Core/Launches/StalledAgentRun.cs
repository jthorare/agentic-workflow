using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>An agent run whose live session ended its turn at least <see cref="LaunchRules.IdleAfter"/> ago without reporting (issue #1000).</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item the launch belongs to.</param>
/// <param name="Instance">The instance whose open run it is; null for a 'run_agent' node's session, which has <see cref="Run"/> and <see cref="Node"/> instead.</param>
/// <param name="TurnEndedAt">When the agent's turn ended.</param>
public sealed record StalledAgentRun(LaunchId Launch, ItemId Item, InstanceAddress? Instance, DateTimeOffset TurnEndedAt)
{
    /// <summary>The graph run a node's session belongs to (PR 7), else null.</summary>
    public string? Run { get; init; }

    /// <summary>The node in that run, else null.</summary>
    public string? Node { get; init; }
}
