using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryWrites;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>
/// Approves a registered recipe hash (engine spec §4.1, §4.2, W8, W9): plans the approval again, so the pins and preview
/// are the build's now, redeems the challenge against that plan's action and subject, then records 'recipe.approved'
/// (through RecipeEvents) and the row. The event comes first so a refused or failed append leaves no row behind. A
/// refused plan or challenge records nothing. Every approval's challenge binds the pins digest, so
/// a child approved while the human reads the preview makes it stale. Re-approving an approved hash with new pins is a
/// drift approval, and it resumes the instances parked on that drift.
/// </summary>
/// <param name="planner">Plans the approval.</param>
/// <param name="challenges">The server's challenges.</param>
/// <param name="events">Appends the registry's events.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="wake">Reaches the running instances.</param>
/// <param name="clock">When the row says it happened.</param>
/// <param name="callers">The caller: the approver and the command id.</param>
internal sealed class ApproveRecipeService(
    RecipeApprovalPlanner planner,
    IChallenges challenges,
    RecipeEvents events,
    IRecipeApprovals approvals,
    IRecipeWake wake,
    TimeProvider clock,
    ICallerContextAccessor callers)
    : IService<ApproveRecipeRequest, ApproveRecipeResponse>
{
    public async ValueTask<Result<ApproveRecipeResponse>> Handle(ApproveRecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("ApproveRecipe needs a caller");
        }

        var plan = await planner.Approve(request.Id, request.Hash, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var redeemed = challenges.Redeem(request.Answer, plan.Value.Action, plan.Value.Subject, WorkflowErrors.RecipeChallengeStale);
        if (redeemed.IsError)
        {
            return redeemed.Error;
        }

        if (plan.Value is { AlreadyApproved: true, Latest: { } standing })
        {
            return await WakeAgain(plan.Value, standing, caller, cancellationToken).ConfigureAwait(false);
        }

        var written = await Record(plan.Value, caller.Actor, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new ApproveRecipeResponse(new CommandAccepted(caller.CommandId, written.Value.Sequence), written.Value.Approval, plan.Value.Preview, plan.Value.Preview.Resumes);
    }

    /// <summary>
    /// The recipe already stands approved with these pins: records nothing and sends the 'approved' wake again, under the
    /// sequence of this hash's own standing approval event (not another recipe's later one), so a drift-parked instance the first wake missed resumes.
    /// </summary>
    private async ValueTask<Result<ApproveRecipeResponse>> WakeAgain(ApprovalPlan plan, RecipeApproval standing, CallerContext caller, CancellationToken cancellationToken)
    {
        var sequence = await events.LatestApproval(plan.Recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (sequence.IsError)
        {
            return sequence.Error;
        }

        var woken = await wake.Wake(new RecipeWakeNotice(plan.Recipe.Hash, RecipeWakeKind.Approved, sequence.Value, caller.Actor), cancellationToken).ConfigureAwait(false);
        return woken.IsError
            ? woken.Error
            : new ApproveRecipeResponse(new CommandAccepted(caller.CommandId, sequence.Value), standing, plan.Preview, plan.Preview.Resumes, AlreadyApproved: true);
    }

    private ValueTask<Result<(RecipeApproval Approval, long Sequence)>> Record(ApprovalPlan plan, string actor, CancellationToken cancellationToken) =>
        new RecipeApprovalRecorder(events, approvals, wake, clock).Record(plan, actor, batch: null, cancellationToken);
}
