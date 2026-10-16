using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// '--accept' and '--reject' on an escalated gate (spec §3.8, §5.5, §5.8, W14), over the real rules: the answer pops the
/// escalation and its resume commits under the answer's key, whether the escalation came from a Step report or a command.
/// A refusal is never a dead end, so a rejected command's escalation ends in SND-WF-004 with the pop committed.
/// </summary>
public sealed class EscalationAnswerTests
{
    private static readonly GateAnswers _modelLow = Gates(("gate.test.model", GateVerdict.Pass, 0.5));
    private static readonly GateAnswers _modelLowCheckFails = Gates(("gate.test.model", GateVerdict.Pass, 0.5), ("gate.test.check", GateVerdict.Fail, null));
    private static readonly GateAnswers _modelLowCheckPasses = Gates(("gate.test.model", GateVerdict.Pass, 0.5), ("gate.test.check", GateVerdict.Pass, null));

    [Fact, Trait("Lane", "unit")]
    public void Accept_on_a_step_escalation_overrides_the_gate_and_applies()
    {
        var recipe = LoopedStep();
        var escalated = Escalated(recipe, Completed(1, "done", ("fact:output", "x")), _modelLow);

        var applied = Assert.IsType<Applied>(Decide(escalated, recipe, Answer(AnswerVariant.Accept), _modelLow, rules: EngineRules.Default));

        var change = applied.Changes[0];
        Assert.Equal("done", change.After.State);
        Assert.Null(change.After.Suspension);
        Assert.Equal(GateOutcome.Overridden, Assert.Single(change.Event.Gates).Outcome);
        Assert.Equal(BlockId.From("gate.test.model"), change.Event.Override?.Gate);
        Assert.Equal("answer", change.Event.Trigger.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Reject_on_a_step_escalation_reruns_the_step_within_its_loop()
    {
        var recipe = LoopedStep();
        var escalated = Escalated(recipe, Completed(1, "done", ("fact:output", "x")), _modelLow);

        var refused = Assert.IsType<Refused>(Decide(escalated, recipe, Answer(AnswerVariant.Reject), _modelLow, rules: EngineRules.Default));

        Assert.Equal((RefusalFollow.Rerun, 2), (refused.Outcome.Follow, refused.Outcome.Round));
        var after = refused.Changes[0].After;
        Assert.Equal(("produce", RunToken.For(1, 2)), (after.State, after.Frame.Run!.Token));
        Assert.Null(after.Suspension);
        Assert.Equal(GateOutcome.Refused, Assert.Single(refused.Changes[0].Event.Gates).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Reject_on_a_command_escalation_commits_the_pop_with_SND_WF_004()
    {
        var recipe = Shipping("gate.test.model");
        var escalated = Escalated(recipe, Ship(), _modelLow);

        var refused = Assert.IsType<Refused>(Decide(escalated, recipe, Answer(AnswerVariant.Reject), _modelLow, rules: EngineRules.Default));

        Assert.Equal((RefusalFollow.CommandRefused, "SND-WF-004"), (refused.Outcome.Follow, refused.Outcome.Error!.Code));
        var change = Assert.Single(refused.Changes);
        Assert.Equal("produce", change.After.State);
        Assert.Null(change.After.Suspension);
        Assert.Equal((WorkflowEventType.Refused, "answer"), (change.Event.Type, change.Event.Trigger.Kind));
        Assert.Equal((RefusalFollow.CommandRefused, "SND-WF-004"), (change.Event.Refusal!.Follow, change.Event.Refusal.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void Accept_on_a_command_escalation_where_another_gate_refuses_commits_the_pop_with_SND_WF_004()
    {
        var recipe = Shipping("gate.test.model", "gate.test.check");
        var escalated = Escalated(recipe, Ship(), _modelLowCheckPasses);

        var refused = Assert.IsType<Refused>(Decide(escalated, recipe, Answer(AnswerVariant.Accept), _modelLowCheckFails, rules: EngineRules.Default));

        Assert.Equal("SND-WF-004", refused.Outcome.Error!.Code);
        var change = Assert.Single(refused.Changes);
        Assert.Equal("produce", change.After.State);
        Assert.Null(change.After.Suspension);
        Assert.Equal([GateOutcome.Overridden, GateOutcome.Refused], change.Event.Gates.Select(g => g.Outcome));
        Assert.Equal(BlockId.From("gate.test.model"), change.Event.Override?.Gate);
    }

    [Fact, Trait("Lane", "unit")]
    public void Accept_on_a_command_escalation_applies_the_command()
    {
        var recipe = Shipping("gate.test.model");
        var escalated = Escalated(recipe, Ship(), _modelLow);

        var applied = Assert.IsType<Applied>(Decide(escalated, recipe, Answer(AnswerVariant.Accept), _modelLow, rules: EngineRules.Default));

        Assert.Equal("done", applied.Changes[0].After.State);
        Assert.Null(applied.Changes[0].After.Suspension);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_command_refused_outside_an_answer_still_changes_no_row()
    {
        var recipe = Shipping("gate.test.check");

        var refused = Assert.IsType<Refused>(Decide(At(recipe, "produce"), recipe, Ship(), Gates(("gate.test.check", GateVerdict.Fail, null)), rules: EngineRules.Default));

        Assert.Equal("SND-WF-004", refused.Outcome.Error!.Code);
        Assert.Empty(refused.Changes);
    }

    /// <summary>PR 232 run 4 (I2): rejecting a gate escalation while an approved drift park is queued behind it still sends the effect that park holds, and lists it on the refusal's event.</summary>
    [Fact, Trait("Lane", "unit")]
    public void Reject_on_a_command_escalation_resends_the_effect_of_an_approved_drift_park_behind_it()
    {
        var recipe = Shipping("gate.test.model");
        var e1 = new EffectIdentity(Id, Entry: 3, Index: 0);
        var held = new RefusedEffect(BlockId.From("effect.claim.ref"), e1, new Dictionary<string, WithValue>(StringComparer.Ordinal), CommandOrigin.Recipe);
        var escalated = Escalated(recipe, Ship(), _modelLow);
        var drifted = Assert.IsType<Applied>(Decide(escalated, recipe, new OutboxRefused("system:dispatcher", held, "SND-RECIPE-006", Sequence: 91), rules: EngineRules.Default)).Changes[0].After;
        var approved = Assert.IsType<Applied>(Decide(drifted, recipe, new RecipeWake("service:engine", RecipeWakeKind.Approved, 8), rules: EngineRules.Default)).Changes[0].After;

        var refused = Assert.IsType<Refused>(Decide(approved, recipe, Answer(AnswerVariant.Reject), _modelLow, rules: EngineRules.Default));

        Assert.Equal([e1], [.. refused.Effects.Select(static e => e.Identity)]);
        Assert.Equal([e1], [.. refused.Changes[0].Event.Effects]);
    }

    private static WorkflowInstance Escalated(Recipe recipe, Trigger trigger, GateAnswers answers)
    {
        var refused = Assert.IsType<Refused>(Decide(At(recipe, "produce"), recipe, trigger, answers, rules: EngineRules.Default));
        Assert.Equal(RefusalFollow.Escalate, refused.Outcome.Follow);
        var after = refused.Changes[0].After;
        Assert.Equal(EngineStates.Escalated, after.State);
        return after;
    }

    private static Recipe LoopedStep() => Minimal(
        [From("done", when: null, "gate.test.model")],
        new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["revisions"] = new(3, LoopSpec.DefaultNoProgress) },
        loop: "revisions");

    private static Recipe Shipping(params string[] gates) => Minimal(
    [
        new(["produce"], "done", "command.ship", When: null,
            [.. gates.Select(g => new GateUse(BlockId.From(g), Threshold: g == "gate.test.model" ? 0.8 : null, new Dictionary<string, WithValue>(StringComparer.Ordinal)))],
            [], [], []),
    ]);

    private static CommandFired Ship() =>
        new("human:thor", CommandVerb.From("ship"), new CommandStamp(CommandNonce.From(new string('a', 32)), Sha("ship")));

    private static AnswerGiven Answer(AnswerVariant variant) =>
        new("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), Sha(variant.ToString())), variant, Text: null, Amount: null, Forwarded: null);
}
