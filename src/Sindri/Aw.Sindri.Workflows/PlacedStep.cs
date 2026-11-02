using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Where a Step runs and the drafter's launch it must differ from.</summary>
/// <param name="Target">Where the Step runs.</param>
/// <param name="Drafter">The drafter's launch; null when the Step differs from none.</param>
internal sealed record PlacedStep(AgentStepTarget Target, DrafterModel? Drafter);
