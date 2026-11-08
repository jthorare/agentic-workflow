using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>The pure effect decisions (engine spec §3.4, §5.7; engine P6): the dispatch recheck. A 'retry' re-emits through the transition decision's own effects.</summary>
public static class EffectRules
{
    /// <summary>The one effect the engine's own origin may send (§5.7): its desk notice.</summary>
    private static readonly BlockId _engineNotice = BlockId.From("effect.notify.desk");
    private static readonly BlockId _engineLineReport = BlockId.From("effect.line.report");   // LN-WF1: a linked instance tells its line it parked, resumed or ended

    /// <summary>
    /// The recheck before an effect's activity (§5.7; unit (b)): null when it may run, else the refusal's code. An
    /// engine-origin effect is exempt from both checks, but only 'effect.notify.desk' may be engine-origin (any other
    /// kind is SND-WF-011); a recipe effect needs its recipe's approval to hold
    /// (SND-RECIPE-005 revoked, SND-RECIPE-006 drifted) and its kind on the allowlist (SND-WF-011).
    /// </summary>
    /// <param name="origin">The effect's origin.</param>
    /// <param name="approval">The recipe's approval check now.</param>
    /// <param name="effects">The approved profile's effect settings now.</param>
    /// <param name="kind">The effect block.</param>
    /// <returns>The refusal code, or null.</returns>
    public static string? Recheck(CommandOrigin origin, ApprovalCheck approval, EffectSettings effects, BlockId kind)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (origin == CommandOrigin.Engine)
        {
            return kind == _engineNotice || kind == _engineLineReport ? null : WorkflowErrors.EffectNotAllowed.Code;   // Engine_origin_limited_to_notify_desk (§10)
        }

        if (approval == ApprovalCheck.Revoked)
        {
            return WorkflowErrors.RecipeRevoked.Code;
        }

        if (approval == ApprovalCheck.Drifted)
        {
            return WorkflowErrors.RecipePinDrift.Code;
        }

        return effects.Allowlist?.Contains(kind) == true ? null : WorkflowErrors.EffectNotAllowed.Code;
    }
}
