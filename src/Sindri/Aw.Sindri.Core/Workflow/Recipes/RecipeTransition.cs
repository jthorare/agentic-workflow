using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A transition in its long form (spec §3.5); the first whose 'when' holds is the matched one.</summary>
/// <param name="From">The non-terminal states it leaves from (the short form is a single name).</param>
/// <param name="To">The state it enters.</param>
/// <param name="On">The trigger: 'step.completed' or 'step.failed' from an active state, 'wait.satisfied' from a wait state, or 'command.&lt;verb&gt;' from any non-terminal state.</param>
/// <param name="When">The closed condition that selects it.</param>
/// <param name="Gates">The gates, run in order.</param>
/// <param name="Evidence">The evidence it requires.</param>
/// <param name="Effects">The effects it emits.</param>
/// <param name="Produces">The facts it records, each an output or fact of the trigger's source (RCP-WIRE-002).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeTransition(
    IReadOnlyList<string> From,
    string To,
    string On,
    WhenClause? When,
    IReadOnlyList<GateUse> Gates,
    IReadOnlyList<EvidenceCheck> Evidence,
    IReadOnlyList<BlockUse> Effects,
    IReadOnlyList<FactKey> Produces);
