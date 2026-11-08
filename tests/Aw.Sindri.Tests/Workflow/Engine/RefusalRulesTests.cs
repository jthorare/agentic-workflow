using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>What follows a refusal (spec §5.5, W5, W14): a re-run, a park, an escalation or SND-WF-004; never a dead end.</summary>
public sealed class RefusalRulesTests
{
    private static readonly GateAnswers _checkFails = Gates(("gate.test.check", GateVerdict.Fail, null));

    [Fact, Trait("Lane", "unit")]
    public void Refused_step_reruns_within_the_loop()
    {
        var recipe = Looped();
        var report = Completed(1, "done", ("fact:output", "x"));

        var refused = Assert.IsType<Refused>(Decide(At(recipe, "produce"), recipe, report, _checkFails));

        Assert.Equal(report.KeyFor(Id), refused.Key);
        Assert.Equal((RefusalFollow.Rerun, 2), (refused.Outcome.Follow, refused.Outcome.Round));
        var after = refused.Changes[0].After;
        Assert.Equal(("produce", 1, RunToken.For(1, 2), 2), (after.State, after.Frame.Entry, after.Frame.Run!.Token, after.Loops["revisions"].Round));
        Assert.Equal((WorkflowEventType.Refused, RefusalFollow.Rerun, 2), (refused.Changes[0].Event.Type, refused.Changes[0].Event.Refusal!.Follow, refused.Changes[0].Event.Refusal!.Round));
        Assert.Equal(RunToken.For(1, 2), Assert.Single(refused.FollowUps).Run);
    }

    [Fact, Trait("Lane", "unit")]
    public void Refused_step_at_the_cap_parks_rounds_with_kind_refusal()
    {
        var recipe = Looped();
        var atCap = At(recipe, "produce", run: 3) with { Loops = Loops(new LoopState(3, 3, Last: null)) };

        var refused = Assert.IsType<Refused>(Decide(atCap, recipe, Completed(1, 3, "done", ("fact:output", "x")), _checkFails));

        var top = AssertParked(refused, ParkReason.Rounds, ParkKind.Refusal, ResumeKind.Rerun);
        Assert.Equal(0, top.Resume.Withheld!.Transition);
    }

    [Fact, Trait("Lane", "unit")]
    public void Refused_step_without_progress_parks_no_progress()
    {
        var recipe = Looped();
        var same = At(recipe, "produce", run: 2) with { Loops = Loops(new LoopState(2, 3, new ProgressMark(Findings: null, Sha("gate.test.check said Fail"), EmptyDiff: false))) };

        var refused = Assert.IsType<Refused>(Decide(same, recipe, Completed(1, 2, "done", ("fact:output", "x")), _checkFails));

        _ = AssertParked(refused, ParkReason.NoProgress, ParkKind.Refusal, ResumeKind.Rerun);
    }

    [Fact, Trait("Lane", "unit")]
    public void Refused_step_without_a_loop_parks_needs_answer()
    {
        var recipe = Minimal([From("done", when: null, "gate.test.check")]);

        var refused = Assert.IsType<Refused>(Decide(At(recipe, "produce"), recipe, Completed(1, "done", ("fact:output", "x")), _checkFails));

        _ = AssertParked(refused, ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun);
    }

    [Fact, Trait("Lane", "unit")]
    public void First_matching_transition_refuses_without_falling_through()
    {
        var recipe = Minimal([From("done", when: null, "gate.test.check"), From("dropped")]);
        var report = Completed(1, "done", ("fact:output", "x"));

        var decision = Decide(At(recipe, "produce"), recipe, report, _checkFails);

        Assert.Equal(0, TransitionDecision.Match(At(recipe, "produce"), recipe, report).Transition);
        Assert.Equal(EngineStates.Parked, Assert.IsType<Refused>(decision).Changes[0].After.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unavailable_gate_refuses()
    {
        var refused = Assert.IsType<Refused>(Decide(At(Item, "triaged"), Item, Routed("startable", outputs: ("fact:stepSpec", "spec"))));

        var gate = Assert.Single(refused.Changes[0].Event.Gates);
        Assert.Equal((BlockId.From("gate.route.startable"), GateOutcome.Unavailable), (gate.Gate, gate.Outcome));
        Assert.Null(gate.Verdict);
        Assert.Equal(RefusalFollow.Park, refused.Outcome.Follow);
    }

    [Fact, Trait("Lane", "unit")]
    public void Refused_wait_parks_needs_answer()
    {
        var routed = Routed("startable", outputs: ("fact:stepSpec", "spec"));

        var refused = Assert.IsType<Refused>(Decide(At(Item, "triaged"), Item, routed, Gates(("gate.route.startable", GateVerdict.Fail, null))));

        var top = AssertParked(refused, ParkReason.NeedsAnswer, ParkKind.Wait, ResumeKind.Reevaluate);
        Assert.Equal(new WithheldTrigger(0, routed), top.Resume.Withheld);
    }

    [Fact, Trait("Lane", "unit")]
    public void Budget_refusal_parks_budget()
    {
        var slot = new WaitSatisfied("service:workflow.inbox", new ResourceWaitSource(ResourceKind.Slot, 3), Outcome: null, new Dictionary<FactKey, FactValue>());

        var refused = Assert.IsType<Refused>(Decide(At(Item, "startable", entry: 2), Item, slot, Gates(("gate.budget.available", GateVerdict.Fail, null))));

        var top = AssertParked(refused, ParkReason.Budget, ParkKind.Wait, ResumeKind.Rewait);
        Assert.Null(top.Resume.Withheld);
        Assert.Empty(refused.Effects);
    }

    [Fact, Trait("Lane", "unit")]
    public void Top_rung_gate_below_threshold_escalates()
    {
        var recipe = Looped("gate.test.model");

        var refused = Assert.IsType<Refused>(Decide(At(recipe, "produce"), recipe, Completed(1, "done", ("fact:output", "x")), Gates(("gate.test.model", GateVerdict.Pass, 0.5))));

        Assert.Equal(RefusalFollow.Escalate, refused.Outcome.Follow);
        var after = refused.Changes[0].After;
        Assert.Equal(EngineStates.Escalated, after.State);
        var top = after.Suspension!; // why: the instance is escalated, asserted just above
        Assert.Equal((EscalationKind.Gate, ResumeKind.Override, BlockId.From("gate.test.model")), (top.Escalation!.Value, top.Resume.Kind, top.Resume.Gate!.Value));
        Assert.Equal(GateOutcome.Escalated, Assert.Single(refused.Changes[0].Event.Gates).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Refused_command_gets_SND_WF_004_and_changes_no_row()
    {
        var recipe = Minimal([new(["produce"], "done", "command.ship", When: null, [new(BlockId.From("gate.test.check"), Threshold: null, new Dictionary<string, WithValue>(StringComparer.Ordinal))], [], [], [])]);
        var ship = new CommandFired("human:thor", CommandVerb.From("ship"), new CommandStamp(CommandNonce.From(new string('a', 32)), Sha("ship")));

        var refused = Assert.IsType<Refused>(Decide(At(recipe, "produce"), recipe, ship, _checkFails));

        Assert.Equal((RefusalFollow.CommandRefused, "SND-WF-004"), (refused.Outcome.Follow, refused.Outcome.Error!.Code));
        Assert.Empty(refused.Changes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_that_misses_its_expectation_refuses()
    {
        var check = new EvidenceCheck("tests", EvidencePoint.Head, EvidenceExpectation.Fail);
        var recipe = Minimal([From("done") with { Evidence = [check] }]);
        GateAnswers Evidence(bool passed) => GateAnswers.None with { Evidence = new Dictionary<EvidenceCheck, EvidenceResult> { [check] = new(passed, ["ran 12 tests"]) } };
        var report = Completed(1, "done", ("fact:output", "x"));

        var refused = Assert.IsType<Refused>(Decide(At(recipe, "produce"), recipe, report, Evidence(passed: true)));
        var applied = Assert.IsType<Applied>(Decide(At(recipe, "produce"), recipe, report, Evidence(passed: false)));

        Assert.False(Assert.Single(refused.Changes[0].Event.Evidence).Held);
        Assert.True(Assert.Single(applied.Changes[0].Event.Evidence).Held);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(Rung.Deterministic, GateVerdict.Uncertain, null, GateOutcome.Refused)]
    [InlineData(Rung.JudgeQuestion, GateVerdict.Pass, 0.5, GateOutcome.Refused)]
    [InlineData(Rung.JudgeQuestion, GateVerdict.Pass, 0.9, GateOutcome.Passed)]
    [InlineData(Rung.BoundedModel, GateVerdict.Uncertain, 0.9, GateOutcome.Escalated)]
    [InlineData(Rung.BoundedModel, GateVerdict.Pass, 0.5, GateOutcome.Escalated)]
    [InlineData(Rung.BoundedModel, GateVerdict.Fail, 0.9, GateOutcome.Refused)]
    public void Gate_answer_means_what_its_rung_and_threshold_say(Rung rung, GateVerdict verdict, double? score, GateOutcome expected)
    {
        var use = new GateUse(BlockId.From("gate.test.model"), Threshold: 0.8, new Dictionary<string, WithValue>(StringComparer.Ordinal));
        var descriptor = TestBlocks.Gate("gate.test.model") with { Rung = rung };

        var record = RefusalRules.Classify(use, new GateResult(verdict, score, ["reason"]), descriptor);

        Assert.Equal((expected, verdict, score, 0.8), (record.Outcome, record.Verdict!.Value, record.Score, record.Threshold!.Value));
    }

    private static Suspension AssertParked(Refused refused, ParkReason reason, ParkKind kind, ResumeKind resume)
    {
        Assert.Equal(RefusalFollow.Park, refused.Outcome.Follow);
        var after = refused.Changes[0].After;
        Assert.Equal((EngineStates.Parked, reason), (after.State, after.Park));
        var top = after.Suspension!; // why: the instance is parked, asserted just above
        Assert.Equal((kind, resume), (top.ParkKind!.Value, top.Resume.Kind));
        Assert.Equal(reason, refused.Changes[0].Event.Refusal!.Park);
        return top;
    }

    private static Recipe Looped(string gate = "gate.test.check") => Minimal(
        [From("done", when: null, gate)],
        new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["revisions"] = new(3, LoopSpec.DefaultNoProgress) },
        loop: "revisions");

    private static Dictionary<string, LoopState> Loops(LoopState state) => new(StringComparer.Ordinal) { ["revisions"] = state };
}
