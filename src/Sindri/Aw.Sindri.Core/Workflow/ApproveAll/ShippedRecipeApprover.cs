using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryWrites;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The work of approve-all (setup-once spec §5.3): registers and approves each recipe of a redeemed plan, in the plan's order, through the services the single 'recipe approve' uses
/// (<see cref="RecipeRegistrar"/>, <see cref="RecipeApprovalPlanner"/>, <see cref="RecipeApprovalRecorder"/>) and with no challenge per recipe: the one hold the <see cref="RedeemedPlan"/> carries is the
/// human's approval, so a plan can't be made without it. For each row it first checks that the row's id@hash is a recipe this build ships (SND-RECIPE-011) at that hash (SND-RECIPE-009). A refusal skips
/// that recipe and every recipe that waits on it, and the work carries on with the rest.
/// </summary>
/// <param name="shipped">The shipped recipes.</param>
/// <param name="registrar">Registers a recipe.</param>
/// <param name="planner">Plans (and so checks) one recipe's approval.</param>
/// <param name="recorder">Records the approval.</param>
/// <param name="store">The registered recipes.</param>
/// <param name="callers">The caller: who approves.</param>
public sealed class ShippedRecipeApprover(
    ShippedRecipeSet shipped,
    RecipeRegistrar registrar,
    RecipeApprovalPlanner planner,
    RecipeApprovalRecorder recorder,
    IRecipeStore store,
    ICallerContextAccessor callers)
{
    /// <summary>Approves each recipe of <paramref name="plan"/>.</summary>
    /// <param name="plan">The plan a challenge was redeemed for.</param>
    /// <param name="progress">Takes one progress line per recipe (its display name).</param>
    /// <param name="cancellationToken">Cancels the work between recipes.</param>
    /// <returns>What was approved, refused and skipped; SND-AUTH-002 when there is no caller to record as the approver.</returns>
    public async ValueTask<Result<ApproveAllResult>> Approve(RedeemedPlan plan, IProgress<string> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(progress);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("approving the shipped recipes needs a caller");
        }

        var files = shipped.Files();
        var hold = new ApprovalBatch(plan.Batch.ToString(), plan.Plan.Digest, plan.HeldMs);
        List<ApproveAllApproved> approved = [];
        List<ApproveAllRefused> refused = [];
        List<ApproveAllSkipped> skipped = [];
        foreach (var row in plan.Plan.Recipes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(row.DisplayName);
            if (row.Children.Cast<RecipeId?>().FirstOrDefault(c => refused.Exists(r => r.Id == c) || skipped.Exists(s => s.Id == c)) is { } because)
            {
                skipped.Add(new ApproveAllSkipped(row.Id, row.DisplayName, because));
            }
            else if (await ApproveOne(row, files.FirstOrDefault(f => f.Id == row.Id), (caller.Actor, hold), cancellationToken).ConfigureAwait(false) is { } refusal)
            {
                refused.Add(refusal);
            }
            else
            {
                approved.Add(new ApproveAllApproved(row.Id, row.DisplayName, row.Hash));
            }
        }

        return new ApproveAllResult(approved, refused, skipped);
    }

    /// <summary>Registers and approves one row; null when it is approved, else why it was refused.</summary>
    private async ValueTask<ApproveAllRefused?> ApproveOne(ApproveAllRow row, ShippedFile? file, (string Actor, ApprovalBatch Batch) hold, CancellationToken cancellationToken)
    {
        var cli = $"sindri-app recipe approve {row.Id.Value}";
        if (file is null)
        {
            return new ApproveAllRefused(row.Id, row.Hash, WorkflowErrors.RecipeNotShipped.Code, $"{row.DisplayName} is not one this build ships.", cli);
        }

        if (file.Hash != row.Hash)
        {
            return new ApproveAllRefused(row.Id, row.Hash, WorkflowErrors.RecipeChallengeStale.Code, $"{row.DisplayName} changed while Sindri was approving. Read the new plan.", cli);
        }

        var failure = await RegisterAndApprove(file, hold, cancellationToken).ConfigureAwait(false);
        return failure is null ? null : new ApproveAllRefused(row.Id, row.Hash, failure.Code, failure.Message, Cli: null);
    }

    private async ValueTask<Error?> RegisterAndApprove(ShippedFile file, (string Actor, ApprovalBatch Batch) hold, CancellationToken cancellationToken)
    {
        var registered = await registrar.Register(file.Text, file.File, cancellationToken).ConfigureAwait(false);
        var stored = registered.IsError ? registered.Error : await store.Find(file.Hash, cancellationToken).ConfigureAwait(false);
        var planned = stored.IsError ? stored.Error
            : stored.Value is { } found ? await planner.Approve(found, cancellationToken).ConfigureAwait(false)
            : WorkflowErrors.RecipeNotFound.With($"{file.Name} is not registered");
        if (planned.IsError)
        {
            return planned.Error;
        }

        if (planned.Value.AlreadyApproved)
        {
            return null;   // it stands approved with these pins already, so there is nothing to write
        }

        var recorded = await recorder.Record(planned.Value, hold.Actor, hold.Batch, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : null;
    }
}
