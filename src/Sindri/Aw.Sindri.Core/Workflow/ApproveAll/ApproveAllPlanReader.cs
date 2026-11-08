using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The shipped recipes' plan, once a profile is approved (setup-once spec §5.3, §7.6): every approval check uses the approved profile's allowlist, so before one
/// there is no plan, no challenge and no approval (SND-SETUP-007). Shared by the plan query, the challenge issue and the approve-all start.
/// </summary>
/// <param name="profile">The approved profile.</param>
/// <param name="shipped">The recipes this build ships.</param>
public sealed class ApproveAllPlanReader(IApprovedProfile profile, IShippedRecipes shipped)
{
    /// <summary>The plan now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The plan; SND-SETUP-007 before a profile is approved.</returns>
    public async ValueTask<Result<ShippedRecipePlan>> Read(CancellationToken cancellationToken)
    {
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        return approved.Value is null
            ? SindriErrors.ProfileMissing.With("approve-all needs a profile file (fix: sindri profile init, then sindri repo add <path>)")
            : await shipped.Plan(cancellationToken).ConfigureAwait(false);
    }
}
