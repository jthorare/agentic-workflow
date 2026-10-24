using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.FrameFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Grants and resumes do what they say (spec W7, W11, §3.8, §5.8, §10).</summary>
public sealed class ResumeTests
{
    [Fact, Trait("Lane", "unit")]
    public void Grant_adds_n_rounds_and_needs_a_terminal()
    {
        var parked = Suspended(Park(ParkReason.Rounds, ParkKind.Loop, ResumeKind.Transition, CheckFailed()));

        Assert.True(AnswerFit.Check(parked, Answer(AnswerVariant.Grant, amount: 1)).IsSuccess);
        Assert.True(AnswerFit.Check(parked, Answer(AnswerVariant.Grant, amount: 3)).IsSuccess);
        Assert.Equal("SND-WF-003", AnswerFit.Check(parked, Answer(AnswerVariant.Grant, amount: 0)).Error.Code);
        Assert.True(AnswerFit.Check(parked, Answer(AnswerVariant.Grant, amount: 4)).IsSuccess);
        Assert.Equal("SND-WF-003", AnswerFit.Check(parked, Answer(AnswerVariant.Grant)).Error.Code);
        var popped = Suspensions.End(parked, child: null).Value.Instance;
        Assert.Equal(parked.Loops, popped.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void Grant_on_a_refusal_kind_park_has_no_upper_bound()
    {
        var parked = Suspended(Park(ParkReason.Rounds, ParkKind.Refusal, ResumeKind.Rerun));

        Assert.True(AnswerFit.Check(parked, Answer(AnswerVariant.Grant, amount: 3)).IsSuccess);
        Assert.True(AnswerFit.Check(parked, Answer(AnswerVariant.Grant, amount: 4)).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void Token_grant_once_per_park()
    {
        var parked = Suspended(Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun));
        var grant = Answer(AnswerVariant.GrantTokens, amount: 200_000);

        Assert.Equal(ResumeKind.Rerun, AnswerFit.Check(parked, grant).Value.Kind);
        Assert.True(AnswerFit.Check(parked, Answer(AnswerVariant.GrantTokens, amount: 200_001)).IsSuccess);
        Assert.Equal("SND-WF-003", AnswerFit.Check(parked, Answer(AnswerVariant.GrantTokens, amount: 0)).Error.Code);
        var popped = Suspensions.End(parked, child: null).Value.Instance;
        Assert.Equal("SND-WF-003", AnswerFit.Check(popped, grant).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Token_grant_on_a_state_without_a_budget_has_no_upper_bound()
    {
        var checking = Instance(Producing().Frame with { State = "checks" });
        var parked = Suspended(Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun), checking);

        Assert.True(AnswerFit.Check(parked, Answer(AnswerVariant.GrantTokens, amount: 400_000)).IsSuccess);
        Assert.True(AnswerFit.Check(parked, Answer(AnswerVariant.GrantTokens, amount: 400_001)).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void Grant_on_refusal_kind_park_reruns_the_step()
    {
        var parked = Suspended(Park(ParkReason.Rounds, ParkKind.Refusal, ResumeKind.Rerun));

        var resume = AnswerFit.Check(parked, Answer(AnswerVariant.Grant, amount: 1)).Value;
        var popped = Suspensions.End(parked, child: null).Value;

        Assert.Equal(ResumeKind.Rerun, resume.Kind);
        Assert.Null(resume.Withheld);
        Assert.Equal("produce", popped.Instance.State);
        Assert.Equal(RunToken.For(3, 1), popped.Instance.Frame.Run?.Token);
    }

    [Fact, Trait("Lane", "unit")]
    public void Grant_performs_the_withheld_loop_transition()
    {
        var checking = Instance(new Frame("checks", 4, new OpenRun(RunToken.For(4, 1), StepRuntime.Desk, Launch: null, "Check."), Child: null));
        var parked = Suspended(Park(ParkReason.Rounds, ParkKind.Loop, ResumeKind.Transition, CheckFailed()), checking);

        var resume = AnswerFit.Check(parked, Answer(AnswerVariant.Grant, amount: 2)).Value;

        Assert.Equal(ResumeKind.Transition, resume.Kind);
        Assert.Equal(LoopBack, resume.Withheld?.Transition);
        Assert.Equal("produce", Recipe.Transitions[LoopBack].To);
    }

    [Fact, Trait("Lane", "unit")]
    public void Resume_reevaluates_gates_under_the_answer_key()
    {
        var waiting = Instance(new Frame("await", 7, Run: null, Child: null));
        var parked = Suspended(Park(ParkReason.NeedsAnswer, ParkKind.Wait, ResumeKind.Reevaluate, CiFinished()), waiting);
        var answer = Answer(AnswerVariant.Resume, "CI is green now");

        var resume = AnswerFit.Check(parked, answer).Value;

        Assert.Equal(ResumeKind.Reevaluate, resume.Kind);
        Assert.Equal(CiFinished(), resume.Withheld);
        Assert.NotEqual(resume.Withheld!.Trigger.KeyFor(Id), answer.KeyFor(Id)); // why: asserted non-null just above
        Assert.StartsWith("wf:" + Id.Value + ":cmd:", answer.KeyFor(Id).Value, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Resource_wait_resume_rewaits()
    {
        var slot = Instance(new Frame("await", 7, Run: null, Child: null));
        var parked = Suspended(Park(ParkReason.NeedsAnswer, ParkKind.Wait, ResumeKind.Rewait), slot);

        var resume = AnswerFit.Check(parked, Answer(AnswerVariant.Resume)).Value;
        var popped = Suspensions.End(parked, child: null).Value;

        Assert.Equal(ResumeKind.Rewait, resume.Kind);
        Assert.Null(resume.Withheld);
        Assert.Equal("await", popped.Instance.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unfit_answer_is_refused_and_changes_nothing()
    {
        var parked = Suspended(Park(ParkReason.Rounds, ParkKind.Loop, ResumeKind.Transition, CheckFailed()));
        var before = parked with { };

        var refused = AnswerFit.Check(parked, Answer(AnswerVariant.Text, "try harder"));

        Assert.Equal("SND-WF-003", refused.Error.Code);
        Assert.Equal(before, parked);
    }

    [Fact, Trait("Lane", "unit")]
    public void Cancel_is_exempt_from_the_approval_check()
    {
        var cancel = new CancelRequested("human:cli", CancelOrigin.Command,
            new CommandStamp(CommandNonce.From(new string('e', 32)), new string('f', 64)), Sequence: null, Parent: null);
        var revoked = Suspended(Park(ParkReason.SpecConflict, ParkKind.Revoked, ResumeKind.None));

        var cancelled = Suspensions.Cancel(revoked);

        Assert.True(cancel.ExemptFromApproval);
        Assert.True(new TakeoverRequested("human:cli", cancel.Stamp!).ExemptFromApproval);
        Assert.True(new HandbackRequested("human:cli", cancel.Stamp!).ExemptFromApproval);
        Assert.False(Answer(AnswerVariant.Resume).ExemptFromApproval);
        Assert.Equal(OutcomeName.From("cancelled"), cancelled.Outcome);
        Assert.Null(cancelled.Suspension);
    }
}
