using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'RunBlockStep' activity returns: a completed run with its stored outputs, or a failure.</summary>
/// <param name="Outcome">The reported outcome; null when the run failed.</param>
/// <param name="Outputs">The facts it recorded, stored.</param>
/// <param name="Decision">The Judge's decision id when the output 'fact:decision' carries one (for the event's 'via block · decision &lt;id&gt;'); else null.</param>
/// <param name="Failure">Why it failed; null when it completed.</param>
internal sealed record BlockStepOutcome(OutcomeName? Outcome, IReadOnlyDictionary<FactKey, FactValue>? Outputs, string? Decision, string? Failure)
{
    /// <summary>The dependency that was down when the Step failed, where the Step named one (<see cref="BlockStepResult.Unavailable"/>); null otherwise.</summary>
    /// <summary>How many images a judge choice looked at (from 'fact:decision'); null for any other Step.</summary>
    public int? ImageCount { get; init; }

    /// <summary>How many images it left out; null when none were.</summary>
    public int? ImagesDropped { get; init; }

    public string? Dependency { get; init; }

    /// <summary>The dependency's own error code, where it had one.</summary>
    public string? Code { get; init; }
}
