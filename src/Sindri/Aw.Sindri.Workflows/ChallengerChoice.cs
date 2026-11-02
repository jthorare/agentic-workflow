using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What <see cref="ChallengerPick"/> answers: the agent the challenger runs on and the drafter it must differ from.</summary>
/// <param name="Agent">The agent the Step launches on.</param>
/// <param name="Drafter">The drafter's launch as the model pick reads it (<see cref="ModelConstraints.Drafter"/>); null when the Step has no drafter launch to differ from.</param>
internal sealed record ChallengerChoice(AgentId Agent, DrafterModel? Drafter);
