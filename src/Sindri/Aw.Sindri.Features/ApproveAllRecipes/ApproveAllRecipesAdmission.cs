using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.ApproveAllRecipes;

/// <summary>
/// The checks an approve-all start makes inside the gate, in the spec's order (setup-once spec §5.3): there is a profile (SND-PROFILE-001), no approve-all job runs
/// (SND-JOB-409 with 'runningJob'), then the plan as it is now is kept for the job under the caller's command id. Rollback drops it when the claim fails. Nothing is
/// written here.
/// </summary>
/// <param name="plans">Reads the plan, refusing before a profile is approved.</param>
/// <param name="registry">The running jobs.</param>
/// <param name="redeemed">Keeps the plan for the job.</param>
/// <param name="callers">The caller, whose command id is the batch of the approvals.</param>
internal sealed class ApproveAllRecipesAdmission(ApproveAllPlanReader plans, IRunningJobs registry, RedeemedPlans redeemed, ICallerContextAccessor callers) : IJobAdmission<ApproveAllRecipesRequest>
{
    /// <summary>The job's kind: the action id.</summary>
    internal static readonly JobKind Kind = JobKind.From("ApproveAllRecipes");

    /// <summary>The job's fixed key: one approve-all at a time.</summary>
    internal static readonly JobKey Key = JobKey.From("approve-all");

    public async ValueTask<Result<ServiceName?>> Admit(ApproveAllRecipesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = await plans.Read(cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        if (registry.IsRunning(Kind, Key, out var running))
        {
            return JobConflicts.Running(Kind, Key, running);
        }

        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("approve-all needs a caller");
        }

        redeemed.Keep(request, RedeemedPlan.For(plan.Value, caller.CommandId));
        return (ServiceName?)null;
    }

    public void Rollback(ApproveAllRecipesRequest request) => redeemed.Drop(request);
}
