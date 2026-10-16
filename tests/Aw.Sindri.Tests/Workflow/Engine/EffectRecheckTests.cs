using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The recheck after the dispatch delay (engine spec rev 8.1 §5.7, §4.3 W11; engine P6 unit (b)): a recipe effect runs
/// only while its recipe's approval holds and its kind is on the approved profile's allowlist. Origin decides the
/// exemption, not the kind, and the engine's origin covers only its own desk notice.
/// </summary>
public sealed class EffectRecheckTests
{
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");
    private static readonly BlockId _claim = BlockId.From("effect.claim.ref");
    private static readonly EffectSettings _allowed = new(new HashSet<BlockId> { _notify, _claim }, EffectSettings.DefaultDispatchDelay);
    private static readonly EffectSettings _none = new(Allowlist: null, EffectSettings.DefaultDispatchDelay);

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_effect_runs_while_its_approval_holds_and_its_kind_is_allowlisted() =>
        Assert.Null(EffectRules.Recheck(CommandOrigin.Recipe, ApprovalCheck.Holds, _allowed, _notify));

    [Fact, Trait("Lane", "unit")]
    public void Revoked_effect_is_refused_at_dispatch() =>
        Assert.Equal("SND-RECIPE-005", EffectRules.Recheck(CommandOrigin.Recipe, ApprovalCheck.Revoked, _allowed, _notify));

    [Fact, Trait("Lane", "unit")]
    public void A_drifted_recipes_effect_is_refused_with_006() =>
        Assert.Equal("SND-RECIPE-006", EffectRules.Recheck(CommandOrigin.Recipe, ApprovalCheck.Drifted, _allowed, _notify));

    [Fact, Trait("Lane", "unit")]
    public void A_kind_off_the_allowlist_is_refused_with_011_and_so_is_every_kind_without_one()
    {
        var notifyOnly = new EffectSettings(new HashSet<BlockId> { _notify }, EffectSettings.DefaultDispatchDelay);

        Assert.Equal("SND-WF-011", EffectRules.Recheck(CommandOrigin.Recipe, ApprovalCheck.Holds, notifyOnly, _claim));
        Assert.Equal("SND-WF-011", EffectRules.Recheck(CommandOrigin.Recipe, ApprovalCheck.Holds, _none, _notify));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_engines_desk_notice_is_exempt_from_the_approval_and_the_allowlist()
    {
        Assert.Null(EffectRules.Recheck(CommandOrigin.Engine, ApprovalCheck.Revoked, _none, _notify));
        Assert.Null(EffectRules.Recheck(CommandOrigin.Engine, ApprovalCheck.Drifted, _none, _notify));
    }

    [Fact, Trait("Lane", "unit")]
    public void Engine_origin_limited_to_notify_desk() =>
        Assert.Equal("SND-WF-011", EffectRules.Recheck(CommandOrigin.Engine, ApprovalCheck.Holds, _allowed, _claim));
}
