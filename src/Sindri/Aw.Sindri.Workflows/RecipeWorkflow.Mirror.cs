using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The ledger mirror of the interpreter (engine spec rev 8 §5.6): one event into the ledger, and whether the instance now waits on a human.</summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>
    /// Mirrors one event into the ledger ('RecordEvent', a local activity keyed by its own activity id; rev 8.1 §5.6),
    /// then sets the search attributes the instance is found by (§5.1) as they stand after it.
    /// </summary>
    private async Task Mirror(WorkflowEventRecord record, bool needsYou)
    {
        var stamped = record with { Tenant = _start!.Tenant, Ring = _start.Ring };   // why: Mirror runs only after RunAsync set _start; the row is the instance's, not the worker's
        _ = await Workflow.ExecuteLocalActivityAsync((RecordEventActivities a) => a.RecordAsync(stamped), RecordEventActivities.LocalOptions).ConfigureAwait(true);
        _mirrored++;   // the instance's version, 'v<n>' in status: Temporal keeps no row version (unit (e))
        Upsert(record.To, needsYou);
    }

    /// <summary>
    /// The instance waits on a human (spec §5.1 'NeedsYou'): it is parked or escalated, or it holds an open desk task. A
    /// takeover doesn't: the human already has it. A park for a dependency that is down doesn't either: nobody can answer it, and health shows it.
    /// </summary>
    private static bool NeedsYou(WorkflowInstance instance) => !instance.Ended && instance.Suspension switch
    {
        { Kind: SuspensionKind.Takeover } or { Kind: SuspensionKind.Park, Reason: ParkReason.Unavailable } => false,
        not null => true,
        _ => instance.Frame.Run is { Runtime: StepRuntime.Desk },
    };
}
