using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'RunBlockStep' activity returns: a completed run with its stored outputs, or a failure.</summary>
/// <param name="Outcome">The reported outcome; null when the run failed.</param>
/// <param name="Outputs">The facts it recorded, stored.</param>
/// <param name="Decision">The Judge's decision id when the output 'fact:decision' carries one (for the event's 'via block · decision &lt;id&gt;'); else null.</param>
/// <param name="Failure">Why it failed; null when it completed.</param>
internal sealed record BlockStepOutcome(OutcomeName? Outcome, IReadOnlyDictionary<FactKey, FactValue>? Outputs, string? Decision, string? Failure);
