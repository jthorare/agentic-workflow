using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The plan of an approval by version or by hash, with a shipped version that waits for its own approval registered first (#983): <see cref="RecipeApprovalPlanner"/>'s lookups behind
/// <see cref="ShippedRecipeRegistration"/>. Without a registration (a host that ships no recipes) it is the planner alone.
/// </summary>
/// <param name="planner">Plans the approval.</param>
/// <param name="registration">Registers a shipped version that is not registered yet; null plans only what is registered.</param>
public sealed class ShippedApprovalPlans(RecipeApprovalPlanner planner, ShippedRecipeRegistration? registration = null)
{
    /// <summary>The plan for 'approve &lt;id&gt; &lt;version&gt;'.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="version">Its version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The plan, or the registration's or the planner's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, int version, CancellationToken cancellationToken)
    {
        var registered = registration is null ? Results.Success(value: false) : await registration.EnsureVersion(id, version, cancellationToken).ConfigureAwait(false);
        return registered.IsError ? registered.Error : await planner.Approve(id, version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The plan for 'approve &lt;id&gt;@&lt;hash&gt;', the hash whole or by a prefix of 8 or more digits.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash or its prefix.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The plan, or the registration's or the planner's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, string hash, CancellationToken cancellationToken)
    {
        var registered = registration is null ? Results.Success(value: false) : await registration.EnsureHash(id, hash, cancellationToken).ConfigureAwait(false);
        return registered.IsError ? registered.Error : await planner.Approve(id, hash, cancellationToken).ConfigureAwait(false);
    }
}
