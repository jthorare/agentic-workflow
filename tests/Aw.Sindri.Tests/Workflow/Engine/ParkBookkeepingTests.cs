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
/// The park bookkeeping (engine spec rev 8.1 §3.8, §5.8; PR 232 review run 2): (a) a refused effect is held by exactly
/// one spec-conflict park until a resume or approval re-sends it; (b) a takeover never drops a park; (c) a revoke or
/// drift wake always ends in a spec-conflict park; (d) a drift approval ends the drift park wherever it sits and re-sends
/// its effects when the instance would otherwise resume. Every case runs the real rules through 'Decide'.
/// </summary>
public sealed class ParkBookkeepingTests
{
    private const string Dispatcher = "system:dispatcher";
    private const string Engine = "service:engine";
    private static readonly EffectIdentity _e1 = new(Id, Entry: 3, Index: 0);
    private static readonly EffectIdentity _e2 = new(Id, Entry: 3, Index: 1);
    private static readonly RefusedEffect _first = new(BlockId.From("effect.claim.ref"), _e1, new Dictionary<string, WithValue>(StringComparer.Ordinal), CommandOrigin.Recipe);
    private static readonly RefusedEffect _second = new(BlockId.From("effect.notify.desk"), _e2, new Dictionary<string, WithValue>(StringComparer.Ordinal), CommandOrigin.Recipe);

    /// <summary>(b) A takeover over a timeout park keeps the dispatch park that waited behind it: the handback and the resume reach it, and its resume re-sends the effect.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_takeover_over_a_timeout_park_with_a_dispatch_park_pending_loses_neither()
    {
        var parked = Refuse(TimedOut(), _first, "SND-WF-011");
        Assert.Equal(ParkKind.Dispatch, parked.PendingPark!.ParkKind);

        var back = Applied(Applied(parked, Takeover()), Handback());
        var sent = ResumeEverything(back);

        Assert.Equal([_e1], sent);
    }

    /// <summary>(a) A refusal under a timeout park, then a takeover and a handback: a dispatch park carrying the effect surfaces.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_refusal_under_a_timeout_park_survives_a_takeover_and_the_handback()
    {
        var taken = Applied(TimedOut(), Takeover());
        var refused = Refuse(taken, _first, "SND-WF-011");

        var back = Applied(refused, Handback());

        Assert.Equal(ParkReason.Timeout, back.Suspension!.Reason);
        Assert.Equal([_e1], ResumeEverything(back));
    }

    /// <summary>(a) A refusal that arrives while the instance is parked for a timeout and taken over after is not 'already parked for drift': a dispatch park carries it.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_dispatch_refusal_under_a_timeout_park_is_held_by_a_dispatch_park_not_absorbed_as_drift()
    {
        var withPending = Refuse(TimedOut(), _first, "SND-WF-011");
        var again = Decide(withPending, Item, new OutboxRefused(Dispatcher, _second, "SND-WF-011", Sequence: 92), rules: EngineRules.Default);

        var after = Assert.IsType<Applied>(again).Changes[0].After;
        Assert.Equal([_e1, _e2], ResumeEverything(after));
    }

    /// <summary>(c) A revoke wake over a timeout park taken over: a revoke park surfaces (it replaces a takeover by design).</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_revoke_wake_while_taken_over_ends_in_a_revoke_park()
    {
        var taken = Applied(TimedOut(), Takeover());

        var revoked = Applied(taken, new RecipeWake(Engine, RecipeWakeKind.Revoked, 7));

        Assert.Equal((ParkReason.SpecConflict, ParkKind.Revoked), (revoked.Suspension!.Reason, revoked.Suspension.ParkKind));
    }

    /// <summary>(c) A drift wake over a timeout park that has a dispatch park pending still parks for drift.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_drift_wake_with_other_parks_present_still_ends_in_a_drift_park()
    {
        var parked = Refuse(TimedOut(), _first, "SND-WF-011");

        var drifted = Applied(parked, new RecipeWake(Engine, RecipeWakeKind.Drifted, 7));

        Assert.Contains(AllParks(drifted), static p => p.ParkKind == ParkKind.Drift);
    }

    /// <summary>(d) A re-approval while the drift park is pending behind a takeover ends it; the handback then resumes and re-sends the effect.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_drift_approval_while_taken_over_ends_the_pending_drift_park_and_the_handback_resends()
    {
        var drifted = Refuse(Claimed(), _first, "SND-RECIPE-006");
        var taken = Applied(drifted, Takeover());

        var approved = Applied(taken, new RecipeWake(Engine, RecipeWakeKind.Approved, 8));
        var back = Assert.IsType<Applied>(Decide(approved, Item, Handback(), rules: EngineRules.Default));

        Assert.Equal(EngineStates.TakenOver, approved.State);
        Assert.Null(back.Changes[0].After.Suspension);
        Assert.Equal([_e1], [.. back.Effects.Select(static e => e.Identity)]);
    }

    /// <summary>(a) Two different effects refused under a timeout park share one dispatch park; the same effect twice is held once.</summary>
    [Fact, Trait("Lane", "unit")]
    public void Two_effects_refused_under_a_timeout_park_are_held_together_and_the_same_one_twice_once()
    {
        var one = Refuse(TimedOut(), _first, "SND-WF-011");
        var two = Refuse(one, _second, "SND-WF-011");

        _ = Assert.IsType<Absorbed>(Decide(two, Item, new OutboxRefused(Dispatcher, _first, "SND-WF-011", Sequence: 95), rules: EngineRules.Default));
        Assert.Equal(1, AllParks(two).Count(static p => p.ParkKind == ParkKind.Dispatch));
        Assert.Equal([_e1, _e2], ResumeEverything(two));
    }

    /// <summary>(a) B1: answering a dispatch park while a drift park waits behind it keeps the dispatch park's effect held; the drift approval then re-sends it exactly once.</summary>
    [Fact, Trait("Lane", "unit")]
    public void Answering_a_dispatch_park_with_a_drift_park_queued_keeps_its_effect_until_the_approval()
    {
        var dispatched = Refuse(Claimed(), _first, "SND-WF-011");
        var drifted = Applied(dispatched, new RecipeWake(Engine, RecipeWakeKind.Drifted, 7));

        var answer = Assert.IsType<Applied>(Decide(drifted, Item, Resume(), rules: EngineRules.Default));
        var answered = answer.Changes[0].After;
        var approved = Assert.IsType<Applied>(Decide(answered, Item, new RecipeWake(Engine, RecipeWakeKind.Approved, 8), rules: EngineRules.Default));

        Assert.Empty(answer.Effects);   // why: the dispatch park's effect stays held; the answer itself sends nothing
        Assert.Equal(ParkKind.Drift, answered.Suspension!.ParkKind);
        Assert.Contains(_e1, Held(answered));
        Assert.Equal([_e1], [.. approved.Effects.Select(static e => e.Identity)]);
        Assert.Null(approved.Changes[0].After.Suspension);
    }

    /// <summary>(a) I2: a child that resumes frees the parent's mirror park; the approved drift park's effect behind it is re-sent exactly once.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_child_release_resends_the_effect_of_an_approved_drift_park_behind_the_mirror()
    {
        var running = At(Item, "running", entry: 4);
        var mirrored = Applied(running with { Frame = running.Frame with { Child = ChildId } }, ChildParked());
        var drifted = Refuse(mirrored, _first, "SND-RECIPE-006");
        var approved = Applied(drifted, new RecipeWake(Engine, RecipeWakeKind.Approved, 8));
        var resumed = new ChildChanged("service:workflow.inbox", ChildId, ChildChange.Resumed, Reason: null, Question: null, TriggerKey.For(ChildId, "cmd:x"));

        var released = Assert.IsType<Applied>(Decide(approved, Item, resumed, rules: EngineRules.Default));

        Assert.Null(released.Changes[0].After.Suspension);
        Assert.Equal([_e1], [.. released.Effects.Select(static e => e.Identity)]);
        Assert.Equal([_e1], [.. released.Changes[0].Event.Effects]);
    }

    /// <summary>(a) PR 232 run 4 (I3): the child's end (wait.satisfied from the child) frees the mirror park; the approved drift park's effect behind it is sent once.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_child_ending_resends_the_effect_of_an_approved_drift_park_behind_the_mirror()
    {
        var approved = MirroredWithApprovedDrift();
        var ended = new WaitSatisfied("service:workflow.inbox", new ChildWaitSource(ChildId, TriggerKey.For(ChildId, "cmd:y")), OutcomeName.From("resolved"), new Dictionary<FactKey, FactValue>());

        var released = Assert.IsType<Applied>(Decide(approved, Item, ended, rules: EngineRules.Default));

        Assert.Equal([_e1], [.. released.Effects.Select(static e => e.Identity)]);
        Assert.Equal([_e1], [.. released.Changes[0].Event.Effects]);
    }

    /// <summary>(a) PR 232 run 4 (I3): a second child park replaces the mirror; the approved drift park's effect behind the old one is sent once and the parent sits on the new mirror.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_second_child_park_replaces_the_mirror_and_sends_the_approved_drift_effect_once()
    {
        var approved = MirroredWithApprovedDrift();
        var again = new ChildChanged("service:workflow.inbox", ChildId, ChildChange.Parked, ParkReason.NoProgress, "again", TriggerKey.For(ChildId, "run:4.1:" + new string('1', 64)));

        var replaced = Assert.IsType<Applied>(Decide(approved, Item, again, rules: EngineRules.Default));

        Assert.Equal([_e1], [.. replaced.Effects.Select(static e => e.Identity)]);
        Assert.Equal([_e1], [.. replaced.Changes[0].Event.Effects]);
        var suspension = replaced.Changes[0].After.Suspension!;
        Assert.Equal((SuspensionKind.Park, ChildId, "again"), (suspension.Kind, suspension.Mirrors, suspension.Question));
    }

    /// <summary>The parent mirroring the child's park, with an approved drift park (holding e1) queued behind it.</summary>
    private static WorkflowInstance MirroredWithApprovedDrift()
    {
        var running = At(Item, "running", entry: 4);
        var mirrored = Applied(running with { Frame = running.Frame with { Child = ChildId } }, ChildParked());
        return Applied(Refuse(mirrored, _first, "SND-RECIPE-006"), new RecipeWake(Engine, RecipeWakeKind.Approved, 8));
    }

    /// <summary>(d) I4a: answering a timeout park with an approved drift park behind it re-sends the drift park's effect once.</summary>
    [Fact, Trait("Lane", "unit")]
    public void Answering_a_timeout_park_resends_the_effect_of_an_approved_drift_park_behind_it()
    {
        var drifted = Refuse(TimedOut(), _first, "SND-RECIPE-006");
        var approved = Applied(drifted, new RecipeWake(Engine, RecipeWakeKind.Approved, 8));

        var answered = Assert.IsType<Applied>(Decide(approved, Item, Resume(), rules: EngineRules.Default));

        Assert.Null(answered.Changes[0].After.Suspension);
        Assert.Equal([_e1], [.. answered.Effects.Select(static e => e.Identity)]);
    }

    /// <summary>(d) I4b: a drift approval with another park queued behind the drift park sends nothing; resuming that park later re-sends the effect once.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_drift_approval_with_a_park_queued_behind_sends_nothing_until_that_park_resumes()
    {
        var drifted = Refuse(Claimed(), _first, "SND-RECIPE-006");
        var withTimeout = Suspensions.Suspend(drifted, DeskTimeouts.TimeoutPark("claimed", TimeSpan.FromDays(2), "service:workflow.timers"));

        var approved = Assert.IsType<Applied>(Decide(withTimeout, Item, new RecipeWake(Engine, RecipeWakeKind.Approved, 8), rules: EngineRules.Default));
        var resumed = Assert.IsType<Applied>(Decide(approved.Changes[0].After, Item, Resume(), rules: EngineRules.Default));

        Assert.Empty(approved.Effects);
        Assert.Equal(ParkReason.Timeout, approved.Changes[0].After.Suspension!.Reason);
        Assert.Equal([_e1], [.. resumed.Effects.Select(static e => e.Identity)]);
        Assert.Null(resumed.Changes[0].After.Suspension);
    }

    /// <summary>(a) S1, the one exception: a revoke replaces every park, the refused effect included; only 'cancel' follows, and nothing re-sends it.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_revoke_replaces_the_dispatch_park_and_its_effect_is_not_re_sent()
    {
        var dispatched = Refuse(Claimed(), _first, "SND-WF-011");

        var revoked = Applied(dispatched, new RecipeWake(Engine, RecipeWakeKind.Revoked, 7));

        Assert.Equal(ParkKind.Revoked, revoked.Suspension!.ParkKind);
        Assert.Empty(Suspensions.PendingParks(revoked));
        Assert.Empty(Held(revoked));
    }

    /// <summary>(I3) An engine notice refused for drift survives a takeover, the approval and the handback as the engine's: it is re-sent with the Engine origin.</summary>
    [Fact, Trait("Lane", "unit")]
    public void An_engine_notice_released_by_the_handback_is_re_sent_with_the_engine_origin()
    {
        var notice = new RefusedEffect(BlockId.From("effect.notify.desk"), _e2, new Dictionary<string, WithValue>(StringComparer.Ordinal), CommandOrigin.Engine);
        var drifted = Refuse(Claimed(), notice, "SND-RECIPE-006");
        var taken = Applied(drifted, Takeover());
        var approved = Applied(taken, new RecipeWake(Engine, RecipeWakeKind.Approved, 8));

        var back = Assert.IsType<Applied>(Decide(approved, Item, Handback(), rules: EngineRules.Default));

        var sent = Assert.Single(back.Effects);
        Assert.Equal((_e2, CommandOrigin.Engine), (sent.Identity, sent.Origin));
    }

    /// <summary>(S2) A refused effect joins the park of its own kind when one exists: a drift refusal goes to the drift park, not to an earlier dispatch park.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_refusal_joins_the_park_of_its_own_kind_before_an_earlier_park_of_another_kind()
    {
        var dispatched = Refuse(TimedOut(), _first, "SND-WF-011");
        var drifted = Applied(dispatched, new RecipeWake(Engine, RecipeWakeKind.Drifted, 7));

        var after = Refuse(drifted, _second, "SND-RECIPE-006");

        var held = AllParks(after).Where(static p => p.ParkKind is ParkKind.Dispatch or ParkKind.Drift).ToDictionary(static p => p.ParkKind!.Value, static p => p.Resume.Effects.Select(static e => e.Identity).ToArray());
        Assert.Equal([_e1], held[ParkKind.Dispatch]);
        Assert.Equal([_e2], held[ParkKind.Drift]);
    }

    /// <summary>The identities of the effects any park of the instance holds.</summary>
    private static IEnumerable<EffectIdentity> Held(WorkflowInstance instance) =>
        AllParks(instance).SelectMany(static p => p.Resume.Effects).Select(static e => e.Identity);

    private static ChildChanged ChildParked() =>
        new("service:workflow.inbox", ChildId, ChildChange.Parked, ParkReason.NoProgress, "same findings twice", TriggerKey.For(ChildId, "run:3.1:abc"));

    private static WorkflowInstance Claimed() => At(Item, "claimed", entry: 3);

    private static WorkflowInstance TimedOut() => Suspensions.Suspend(Claimed(), DeskTimeouts.TimeoutPark("claimed", TimeSpan.FromDays(2), "service:workflow.timers"));

    private static WorkflowInstance Refuse(WorkflowInstance instance, RefusedEffect effect, string code) =>
        Applied(instance, new OutboxRefused(Dispatcher, effect, code, Sequence: 91));

    private static WorkflowInstance Applied(WorkflowInstance instance, Trigger trigger) =>
        Assert.IsType<Applied>(Decide(instance, Item, trigger, rules: EngineRules.Default)).Changes[0].After;

    /// <summary>Every park the instance holds, current first.</summary>
    private static IEnumerable<Suspension> AllParks(WorkflowInstance instance) =>
        Suspensions.PendingParks(instance).Prepend(instance.Suspension!).Where(static p => p.Kind == SuspensionKind.Park);

    /// <summary>Answers '--resume' until the instance runs again and returns the identities it re-sent, in order.</summary>
    private static EffectIdentity[] ResumeEverything(WorkflowInstance start)
    {
        var sent = new List<EffectIdentity>();
        var instance = start;
        for (var round = 0; round < 6 && instance.Suspension is not null; round++)
        {
            var applied = Assert.IsType<Applied>(Decide(instance, Item, Resume(), rules: EngineRules.Default));
            sent.AddRange(applied.Effects.Select(static e => e.Identity));
            instance = applied.Changes[0].After;
        }

        Assert.Null(instance.Suspension);
        return [.. sent];
    }

    private static AnswerGiven Resume() =>
        new("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), Sha("resume")), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

    private static TakeoverRequested Takeover() => new("human:cli", Stamp('a'));

    private static HandbackRequested Handback() => new("human:cli", Stamp('b'));

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));
}
