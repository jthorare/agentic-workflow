using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The registry's wakes (engine spec rev 8.1 §4.1, §5.8; engine P6 integration): a revoke parks the instance at once, a
/// drift parks it for the re-approval, and the re-approval ('recipe.approved') resumes a drift park and re-sends the
/// effect that park carries (the one refused for the drift), never any other park.
/// </summary>
public sealed class RecipeWakeTests
{
    private const string Dispatcher = "system:dispatcher";
    private const string Engine = "service:engine";
    private static readonly EffectIdentity _claim = new(Id, Entry: 3, Index: 0);
    private static readonly RefusedEffect _claimRef = new(BlockId.From("effect.claim.ref"), _claim, new Dictionary<string, WithValue>(StringComparer.Ordinal), CommandOrigin.Recipe);

    [Fact, Trait("Lane", "unit")]
    public void A_drift_approval_resumes_a_drift_park_and_resends_the_refused_effect()
    {
        var parked = DriftParked();

        var resumed = Assert.IsType<Applied>(Decide(parked, Item, Wake(RecipeWakeKind.Approved), rules: EngineRules.Default));

        var effect = Assert.Single(resumed.Effects);
        Assert.Equal((BlockId.From("effect.claim.ref"), _claim, CommandOrigin.Recipe), (effect.Effect, effect.Identity, effect.Origin));
        Assert.Null(resumed.Changes[0].After.Suspension);
        Assert.Equal([_claim], resumed.Changes[0].Event.Effects);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_drift_approval_with_no_effect_on_the_park_resumes_it_and_sends_nothing()
    {
        var parked = Parked(Decide(Claimed(), Item, Wake(RecipeWakeKind.Drifted), rules: EngineRules.Default));

        var resumed = Assert.IsType<Applied>(Decide(parked, Item, Wake(RecipeWakeKind.Approved, sequence: 8), rules: EngineRules.Default));

        Assert.Empty(resumed.Effects);
        Assert.Null(resumed.Changes[0].After.Suspension);
    }

    /// <summary>PR 232 run 4 (I1): the same approval wake sent again (a re-approval with the same pins) resumes a drift park the first one missed.</summary>
    [Fact, Trait("Lane", "unit")]
    public void The_approval_wake_sent_again_resumes_a_drift_park_the_first_one_missed()
    {
        var missed = Claimed();
        _ = Assert.IsType<Absorbed>(Decide(missed, Item, Wake(RecipeWakeKind.Approved, sequence: 7), rules: EngineRules.Default));   // why: absorbed before the drift park exists
        var drifted = Parked(Decide(missed, Item, new OutboxRefused(Dispatcher, _claimRef, "SND-RECIPE-006", Sequence: 91), rules: EngineRules.Default));   // why: the same instance the first wake missed

        var again = Assert.IsType<Applied>(Decide(drifted, Item, Wake(RecipeWakeKind.Approved, sequence: 7), rules: EngineRules.Default));

        Assert.Null(again.Changes[0].After.Suspension);
        Assert.Equal([_claim], [.. again.Effects.Select(static e => e.Identity)]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_approval_leaves_every_other_park_alone()
    {
        var other = Parked(Decide(Claimed(), Item, new OutboxRefused(Dispatcher, _claimRef, "SND-WF-011", Sequence: 91), rules: EngineRules.Default));

        _ = Assert.IsType<Absorbed>(Decide(other, Item, Wake(RecipeWakeKind.Approved), rules: EngineRules.Default));
        _ = Assert.IsType<Absorbed>(Decide(Claimed(), Item, Wake(RecipeWakeKind.Approved), rules: EngineRules.Default));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_revoke_parks_at_once_with_only_cancel_and_twice_parks_once()
    {
        var parked = Parked(Decide(Claimed(), Item, Wake(RecipeWakeKind.Revoked), rules: EngineRules.Default));

        Assert.Equal((ParkReason.SpecConflict, ParkKind.Revoked, ResumeKind.None), (parked.Suspension!.Reason, parked.Suspension.ParkKind, parked.Suspension.Resume.Kind));
        _ = Assert.IsType<Absorbed>(Decide(parked, Item, Wake(RecipeWakeKind.Revoked, sequence: 8), rules: EngineRules.Default));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_drift_wake_parks_drift_and_twice_parks_once()
    {
        var parked = Parked(Decide(Claimed(), Item, Wake(RecipeWakeKind.Drifted), rules: EngineRules.Default));

        Assert.Equal((ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm), (parked.Suspension!.Reason, parked.Suspension.ParkKind, parked.Suspension.Resume.Kind));
        _ = Assert.IsType<Absorbed>(Decide(parked, Item, Wake(RecipeWakeKind.Drifted, sequence: 8), rules: EngineRules.Default));
    }

    /// <summary>Two effects of one transition are refused under the same drift: the park keeps both, and the approval re-sends both.</summary>
    [Fact, Trait("Lane", "unit")]
    public void Two_effects_refused_under_drift_are_both_resent_by_the_approval()
    {
        var second = new RefusedEffect(BlockId.From("effect.notify.desk"), new EffectIdentity(Id, Entry: 3, Index: 1), new Dictionary<string, WithValue>(StringComparer.Ordinal), CommandOrigin.Recipe);
        var parked = Parked(Decide(DriftParked(), Item, new OutboxRefused(Dispatcher, second, "SND-RECIPE-006", Sequence: 92), rules: EngineRules.Default));

        var resumed = Assert.IsType<Applied>(Decide(parked, Item, Wake(RecipeWakeKind.Approved), rules: EngineRules.Default));

        Assert.Equal([_claim, second.Identity], resumed.Effects.Select(e => e.Identity));
        Assert.Equal([_claim, second.Identity], resumed.Changes[0].Event.Effects);
    }

    /// <summary>A drift wake parks with no effect; the refusal that follows (the delay expired) joins that park, and the approval sends it.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_refusal_after_a_drift_wake_park_is_kept_and_resent_by_the_approval()
    {
        var woken = Parked(Decide(Claimed(), Item, Wake(RecipeWakeKind.Drifted), rules: EngineRules.Default));

        var parked = Parked(Decide(woken, Item, new OutboxRefused(Dispatcher, _claimRef, "SND-RECIPE-006", Sequence: 92), rules: EngineRules.Default));
        var resumed = Assert.IsType<Applied>(Decide(parked, Item, Wake(RecipeWakeKind.Approved, sequence: 9), rules: EngineRules.Default));

        Assert.Equal(_claim, Assert.Single(resumed.Effects).Identity);
    }

    /// <summary>The same effect refused twice under drift changes nothing.</summary>
    [Fact, Trait("Lane", "unit")]
    public void The_same_effect_refused_twice_under_drift_is_absorbed()
    {
        var parked = Decide(DriftParked(), Item, new OutboxRefused(Dispatcher, _claimRef, "SND-RECIPE-006", Sequence: 92), rules: EngineRules.Default);

        _ = Assert.IsType<Absorbed>(parked);
    }

    private static WorkflowInstance Claimed() => At(Item, "claimed", entry: 3);

    private static WorkflowInstance DriftParked() =>
        Parked(Decide(Claimed(), Item, new OutboxRefused(Dispatcher, _claimRef, "SND-RECIPE-006", Sequence: 91), rules: EngineRules.Default));

    private static RecipeWake Wake(RecipeWakeKind kind, long sequence = 7) => new(Engine, kind, sequence);

    private static WorkflowInstance Parked(Decision decision) => Assert.IsType<Applied>(decision).Changes[0].After;
}
