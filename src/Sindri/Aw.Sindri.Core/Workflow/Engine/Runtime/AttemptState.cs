using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>An attempt of a '(recipe, item)' pair as <see cref="IInstanceDirectory"/> found it (engine spec §5.1).</summary>
/// <param name="Address">The attempt's address ('T-1/step/2').</param>
/// <param name="Attempt">Its number, from 1.</param>
/// <param name="Live">Whether its workflow is still running.</param>
public sealed record AttemptState(InstanceAddress Address, int Attempt, bool Live);
