using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>What a human trigger's approval check answers, as the error it refuses with (SND-RECIPE-005, -006); shared by every slice that sends one.</summary>
public static class ApprovalRefusal
{
    /// <summary>Success when the approval holds, else the refusal naming what changed and how to go on.</summary>
    /// <param name="check">The check's answer.</param>
    /// <param name="recipe">The instance's recipe id.</param>
    /// <param name="hash">The hash the instance is pinned to.</param>
    /// <param name="address">The instance.</param>
    /// <returns>Unit, or the refusal.</returns>
    public static Result<Unit> Of(ApprovalCheck check, RecipeId recipe, RecipeHash hash, InstanceAddress address) => check switch
    {
        ApprovalCheck.Revoked => WorkflowErrors.RecipeRevoked.With($"{recipe.Value}@{hash.Display} was revoked; nothing was sent (fix: approve a new version, or sindri workflow cancel {address.Value})"),
        ApprovalCheck.Drifted => WorkflowErrors.RecipePinDrift.With($"a block {recipe.Value}@{hash.Display} pins changed since approval; nothing was sent (fix: re-approve it, see sindri recipe list --stale)"),
        ApprovalCheck.Holds or _ => Unit.Value,
    };
}
