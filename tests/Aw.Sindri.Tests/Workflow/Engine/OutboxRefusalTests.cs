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
/// 'outbox.refused' (engine spec rev 8.1 §5.7, §3.8; engine P6 unit (b)): the recheck refused one of the instance's
/// effects, and the instance parks with spec-conflict of the code's kind. A revoked recipe offers only 'cancel'; a kind
/// off the allowlist parks 'dispatch', whose 'answer --resume' re-emits the effect under its identity; drift parks
/// 'drift'. The same revoke twice parks once.
/// </summary>
public sealed class OutboxRefusalTests
{
    private const string Dispatcher = "system:dispatcher";
    private static readonly EffectIdentity _claim = new(Id, Entry: 3, Index: 0);   // startable -> claimed emits effect.claim.ref
    private static readonly RefusedEffect _claimRef = new(BlockId.From("effect.claim.ref"), _claim, new Dictionary<string, WithValue>(StringComparer.Ordinal), CommandOrigin.Recipe);

    [Fact, Trait("Lane", "unit")]
    public void A_revoked_refusal_parks_spec_conflict_revoked_with_only_cancel()
    {
        var parked = Parked(Decide(Claimed(), Item, Refused("SND-RECIPE-005"), rules: EngineRules.Default));

        Assert.Equal((ParkReason.SpecConflict, ParkKind.Revoked, ResumeKind.None), Of(parked));
        Assert.Equal("SND-WF-003", AnswerFit.Check(parked, Item, Resume()).Error.Code);
        Assert.Equal("cancelled", Assert.IsType<Applied>(Decide(parked, Item, Cancel(), rules: EngineRules.Default)).Changes[0].After.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_revoke_twice_parks_once()
    {
        var parked = Parked(Decide(Claimed(), Item, Refused("SND-RECIPE-005"), rules: EngineRules.Default));

        _ = Assert.IsType<Absorbed>(Decide(parked, Item, Refused("SND-RECIPE-005", sequence: 92), rules: EngineRules.Default));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_drift_refusal_parks_spec_conflict_drift()
    {
        var parked = Parked(Decide(Claimed(), Item, Refused("SND-RECIPE-006"), rules: EngineRules.Default));

        Assert.Equal((ParkReason.SpecConflict, ParkKind.Drift), (parked.Suspension!.Reason, parked.Suspension.ParkKind));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_kind_off_the_allowlist_parks_dispatch_naming_the_effect()
    {
        var parked = Parked(Decide(Claimed(), Item, Refused("SND-WF-011"), rules: EngineRules.Default));

        Assert.Equal((ParkReason.SpecConflict, ParkKind.Dispatch, ResumeKind.Reemit), Of(parked));
        Assert.Equal(_claimRef, parked.Suspension!.Resume.Effects.Single());
        Assert.StartsWith("effect.claim.ref isn't on the profile's effect allowlist", parked.Suspension.Question, StringComparison.Ordinal);
    }

    /// <summary>A setup the effect can't run in (no commit, a repo the profile lacks, no fact:item) parks dispatch and names the cause, not the allowlist.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_setup_failure_parks_dispatch_naming_the_cause_and_resume_reemits()
    {
        const string Cause = "/repos/dev has no commit to point refs/sindri/me/T-4 at";
        var failed = new OutboxRefused(Dispatcher, _claimRef, "SND-WF-010", Sequence: 94, Cause);

        var parked = Parked(Decide(Claimed(), Item, failed, rules: EngineRules.Default));
        var resumed = Assert.IsType<Applied>(Decide(parked, Item, Resume(), rules: EngineRules.Default));

        Assert.Equal((ParkReason.SpecConflict, ParkKind.Dispatch, ResumeKind.Reemit), Of(parked));
        Assert.Contains(Cause, parked.Suspension!.Question, StringComparison.Ordinal);
        Assert.DoesNotContain("allowlist", parked.Suspension.Question, StringComparison.Ordinal);
        Assert.Equal(_claim, Assert.Single(resumed.Effects).Identity);
    }

    /// <summary>The park's transition records the refusal (code and message) so the needs-you row can say why.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_refusal_park_records_the_code_and_message_as_the_transitions_reason()
    {
        const string Cause = "fact:nextPhase must be { phase, attempt, packet, base, stackParent } with a lowercase phase id and an attempt from 1";
        var applied = Assert.IsType<Applied>(Decide(Claimed(), Item, new OutboxRefused(Dispatcher, _claimRef, "SND-WF-010", Sequence: 95, Cause), rules: EngineRules.Default));

        Assert.Equal("SND-WF-010: " + Cause, applied.Changes[0].Event.Trigger.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_refusal_park_without_a_message_records_just_the_code()
    {
        var applied = Assert.IsType<Applied>(Decide(Claimed(), Item, Refused("SND-WF-011"), rules: EngineRules.Default));

        Assert.Equal("SND-WF-011", applied.Changes[0].Event.Trigger.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void Resume_on_a_dispatch_park_reemits_the_effect_under_its_identity()
    {
        var parked = Parked(Decide(Claimed(), Item, Refused("SND-WF-011"), rules: EngineRules.Default));

        var resumed = Assert.IsType<Applied>(Decide(parked, Item, Resume(), rules: EngineRules.Default));

        var effect = Assert.Single(resumed.Effects);
        Assert.Equal((BlockId.From("effect.claim.ref"), _claim, CommandOrigin.Recipe), (effect.Effect, effect.Identity, effect.Origin));
        Assert.Null(resumed.Changes[0].After.Suspension);
        Assert.Equal("claimed", resumed.Changes[0].After.State);
        Assert.Equal([_claim], resumed.Changes[0].Event.Effects);
    }

    /// <summary>
    /// The refusal carries the effect as it was emitted, so an instance that has entered another frame since (here
    /// 'claimed' at entry 5, while the effect was emitted at entry 2 with arguments no transition into 'claimed' has)
    /// still parks naming it, and '--resume' re-sends exactly it.
    /// </summary>
    [Fact, Trait("Lane", "unit")]
    public void A_refusal_from_an_earlier_frame_parks_naming_its_effect_and_resume_resends_exactly_it()
    {
        var earlier = new RefusedEffect(
            BlockId.From("effect.notify.desk"), new EffectIdentity(Id, Entry: 2, Index: 1),
            new Dictionary<string, WithValue>(StringComparer.Ordinal) { ["to"] = new WithValue(Literal: null, Fact: null, Profile: "user") }, CommandOrigin.Recipe);
        var moved = At(Item, "claimed", entry: 5);

        var parked = Parked(Decide(moved, Item, new OutboxRefused(Dispatcher, earlier, "SND-WF-011", Sequence: 93), rules: EngineRules.Default));
        var resumed = Assert.IsType<Applied>(Decide(parked, Item, Resume(), rules: EngineRules.Default));

        Assert.StartsWith("effect.notify.desk isn't on the profile's effect allowlist", parked.Suspension!.Question, StringComparison.Ordinal);
        var effect = Assert.Single(resumed.Effects);
        Assert.Equal((earlier.Kind, earlier.Identity, CommandOrigin.Recipe), (effect.Effect, effect.Identity, effect.Origin));
        Assert.Equal(earlier.With, effect.With);
    }

    /// <summary>An engine notice refused at run time (SND-WF-015 after exhaustion) and resumed is re-sent as the engine's, not as a recipe effect (PR 232 review run 2, I3).</summary>
    [Fact, Trait("Lane", "unit")]
    public void An_engine_origin_effect_is_resent_with_engine_origin_after_the_resume()
    {
        var notice = new RefusedEffect(
            BlockId.From("effect.notify.desk"), new EffectIdentity(Id, Entry: 3, Index: 5), new Dictionary<string, WithValue>(StringComparer.Ordinal), CommandOrigin.Engine);
        var parked = Parked(Decide(Claimed(), Item, new OutboxRefused(Dispatcher, notice, "SND-WF-015", Sequence: 94), rules: EngineRules.Default));

        var resumed = Assert.IsType<Applied>(Decide(parked, Item, Resume(), rules: EngineRules.Default));

        Assert.Equal(CommandOrigin.Engine, Assert.Single(resumed.Effects).Origin);
    }

    /// <summary>The same holds for the effect a drift park re-sends on the re-approval.</summary>
    [Fact, Trait("Lane", "unit")]
    public void An_engine_origin_effect_refused_for_drift_is_resent_with_engine_origin_on_the_approval()
    {
        var notice = new RefusedEffect(
            BlockId.From("effect.notify.desk"), new EffectIdentity(Id, Entry: 3, Index: 5), new Dictionary<string, WithValue>(StringComparer.Ordinal), CommandOrigin.Engine);
        var drifted = Parked(Decide(Claimed(), Item, new OutboxRefused(Dispatcher, notice, "SND-RECIPE-006", Sequence: 94), rules: EngineRules.Default));

        var approved = Assert.IsType<Applied>(Decide(drifted, Item, new RecipeWake("service:engine", RecipeWakeKind.Approved, 8), rules: EngineRules.Default));

        Assert.Equal(CommandOrigin.Engine, Assert.Single(approved.Effects).Origin);
    }

    private static WorkflowInstance Claimed() => At(Item, "claimed", entry: 3);

    private static OutboxRefused Refused(string code, long sequence = 91) => new(Dispatcher, _claimRef, code, sequence);

    private static AnswerGiven Resume() =>
        new("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), Sha("resume")), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

    private static CancelRequested Cancel() =>
        new("human:cli", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string('d', 32)), Sha("cancel")), Sequence: null, Parent: null);

    private static WorkflowInstance Parked(Decision decision) => Assert.IsType<Applied>(decision).Changes[0].After;

    private static (ParkReason?, ParkKind?, ResumeKind) Of(WorkflowInstance parked) =>
        (parked.Suspension!.Reason, parked.Suspension.ParkKind, parked.Suspension.Resume.Kind);
}
