using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>The trigger an event records (spec §5.6): its kind, its id (the key) and its actor, and the source's outcome.</summary>
/// <param name="Kind">The trigger's name ('step.completed', 'answer', 'deadline' …).</param>
/// <param name="Key">'wf:{instance}:{trigger}'.</param>
/// <param name="Actor">Who fired it.</param>
/// <param name="Outcome">The source's outcome, for a Step report or a satisfied wait.</param>
/// <param name="Reason">A step.failed's reason, scrubbed like the rest of the payload: what 'sameFailure' compares (§3.7) and '--record' writes back (§9.2).</param>
/// <param name="Via">'block' when the engine ran the Step on the block runtime (DT spec §8.2); null for a desk or any other trigger.</param>
/// <param name="Decision">The Judge decision id of a block Step's result, for 'sindri workflow why'; null otherwise.</param>
public sealed record TriggerRecord(string Kind, TriggerKey Key, string Actor, OutcomeName? Outcome, string? Reason = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Decision = null);
