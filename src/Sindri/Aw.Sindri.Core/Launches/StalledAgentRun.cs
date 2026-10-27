using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>An agent run whose live session ended its turn at least <see cref="LaunchRules.IdleAfter"/> ago without reporting (issue #1000).</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item the launch belongs to.</param>
/// <param name="Instance">The instance whose open run it is.</param>
/// <param name="TurnEndedAt">When the agent's turn ended.</param>
public sealed record StalledAgentRun(LaunchId Launch, ItemId Item, InstanceAddress Instance, DateTimeOffset TurnEndedAt);
