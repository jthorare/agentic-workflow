using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.RegistryWrites;

/// <summary>
/// Records an approved plan (engine spec §4.1, W9): the event, then the row, then (for a drift approval) the wake of the instances parked on that drift. The one place both ApproveRecipe
/// and the approve-all job write an approval, so the two cannot differ in what an approval is. The event comes first so a refused or failed append leaves no row behind.
/// </summary>
/// <param name="events">Appends the registry's events.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="wake">Reaches the running instances.</param>
/// <param name="clock">When the row says it happened.</param>
public sealed class RecipeApprovalRecorder(RecipeEvents events, IRecipeApprovals approvals, IRecipeWake wake, TimeProvider clock)
{
    /// <summary>Records <paramref name="plan"/> as approved by <paramref name="actor"/>.</summary>
    /// <param name="plan">The plan the planner made (its checks passed).</param>
    /// <param name="actor">Who approved.</param>
    /// <param name="batch">The approve-all hold it belongs to, or null.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored approval and the ledger sequence of its event; or the refusal of the write.</returns>
    public async ValueTask<Result<(RecipeApproval Approval, long Sequence)>> Record(ApprovalPlan plan, string actor, ApprovalBatch? batch, CancellationToken cancellationToken)
    {
        var recipe = plan.Recipe;
        var row = new RecipeApproval(recipe.Id, recipe.Version, recipe.Hash, actor, clock.GetUtcNow(), ApprovalVia.Desk, recipe.Base, plan.Preview.Pins);
        var appended = await events.Approved(row, plan.Reapproval, batch, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var stored = await approvals.Approve(row, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var sequence = appended.Value.Sequence;
        var woken = plan.Reapproval
            ? await wake.Wake(new RecipeWakeNotice(recipe.Hash, RecipeWakeKind.Approved, sequence, actor), cancellationToken).ConfigureAwait(false)
            : Unit.Value;
        return woken.IsError ? woken.Error : (stored.Value, sequence);
    }
}
