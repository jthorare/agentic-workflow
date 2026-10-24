using Aw.Kernel;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.ApproveAllRecipes;

/// <summary>
/// The recipes this build ships and where they stand (setup-once spec §5.3), over the embedded set, the registry and the approvals: the plan is <see cref="ShippedRecipePlanner"/>'s, and applying a redeemed
/// plan is <see cref="ShippedRecipeApprover"/>'s. The one implementation of <see cref="IShippedRecipes"/>; the fake in the test kit honours the same contract.
/// </summary>
/// <param name="planner">Computes the plan.</param>
/// <param name="approver">Applies a redeemed plan.</param>
internal sealed class ShippedRecipes(ShippedRecipePlanner planner, ShippedRecipeApprover approver) : IShippedRecipes
{
    public ValueTask<Result<ShippedRecipePlan>> Plan(CancellationToken cancellationToken) => planner.Plan(cancellationToken);

    public ValueTask<Result<ApproveAllResult>> Approve(RedeemedPlan plan, IProgress<string> progress, CancellationToken cancellationToken) =>
        approver.Approve(plan, progress, cancellationToken);
}
