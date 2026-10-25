using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What <see cref="ChallengerPick"/> is asked.</summary>
/// <param name="Item">The instance's item.</param>
/// <param name="Instance">The instance the Step runs in.</param>
/// <param name="Launch">The Step run's launch id; a launch already recorded under it keeps its agent.</param>
/// <param name="DrafterState">The state id of the Step this one must differ from, in the same instance.</param>
/// <param name="Default">The agent the profile names for every Step (workflow.agent.target); used when there is no drafter launch to differ from.</param>
internal sealed record ChallengerAsk(ItemId Item, InstanceAddress Instance, LaunchId Launch, string DrafterState, AgentId Default);
