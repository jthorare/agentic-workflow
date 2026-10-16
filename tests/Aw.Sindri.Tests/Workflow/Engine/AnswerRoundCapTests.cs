using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A re-run an answer starts counts a round of the state's loop (spec §3.7, §3.8, §5.8, W7): below the cap it runs; at
/// the cap it parks with rounds (kind refusal, the withheld trigger kept), so '--grant n' is the only way past the cap
/// (§13: a desk '--text' can't add rounds without end). Every answer's event carries the grant it made (§5.2).
/// </summary>
public sealed class AnswerRoundCapTests
{
    [Fact, Trait("Lane", "unit")]
    public void Text_on_a_needs_answer_park_below_the_cap_reruns_the_step()
    {
        var parked = Parked(In("produce", new LoopState(2, 3, Last: null)), ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun);

        var applied = Assert.IsType<Applied>(Decide(parked, Answer(AnswerVariant.Text, text: "Use the assembly version.")));

        var after = applied.Changes[0].After;
        Assert.Equal(("produce", RunToken.For(3, 2), 3), (after.State, after.Frame.Run!.Token, after.Loops[Revisions].Round));
        Assert.Equal(FollowUpKind.StartRun, Assert.Single(applied.FollowUps).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_on_a_needs_answer_park_at_the_cap_parks_rounds_instead_of_running()
    {
        var withheld = new WithheldTrigger(Transition: null, Failed());
        var parked = Parked(In("produce", new LoopState(3, 3, Last: null)), ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun, withheld);

        var applied = Assert.IsType<Applied>(Decide(parked, Answer(AnswerVariant.Text, text: "Try again.")));

        AssertParkedAtCap(applied, withheld);
    }

    [Fact, Trait("Lane", "unit")]
    public void Token_grant_on_a_budget_park_at_the_cap_parks_rounds_instead_of_running()
    {
        var withheld = new WithheldTrigger(0, Failed());
        var parked = Parked(In("produce", new LoopState(3, 3, Last: null), Spent(200_000, 200_000)), ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun, withheld);

        var applied = Assert.IsType<Applied>(Decide(parked, Answer(AnswerVariant.GrantTokens, amount: 50_000)));

        AssertParkedAtCap(applied, withheld);
        Assert.Equal(50_000, applied.Changes[0].After.Spent.Granted);
        Assert.Equal(50_000, applied.Changes[0].Event.Grant?.Tokens);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_grant_on_the_rounds_park_is_the_way_past_the_cap()
    {
        var parked = Parked(In("produce", new LoopState(3, 3, Last: null)), ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun);
        var capped = Assert.IsType<Applied>(Decide(parked, Answer(AnswerVariant.Text, text: "Try again."))).Changes[0].After;

        var applied = Assert.IsType<Applied>(Decide(capped, Answer(AnswerVariant.Grant, amount: 1)));

        var after = applied.Changes[0].After;
        Assert.Equal(("produce", new LoopState(4, 4, Last: null)), (after.State, after.Loops[Revisions]));
        Assert.Equal(RunToken.For(3, 2), after.Frame.Run!.Token);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pending_park_that_takes_effect_records_the_grant_on_its_event()
    {
        var rounds = Parked(In("produce", new LoopState(3, 3, Last: null)), ParkReason.Rounds, ParkKind.Refusal, ResumeKind.Rerun);
        var pending = rounds with { PendingPark = BudgetPark };

        var applied = Assert.IsType<Applied>(Decide(pending, Answer(AnswerVariant.Grant, amount: 2)));

        var change = applied.Changes[0];
        Assert.Equal((EngineStates.Parked, ParkReason.Budget), (change.After.State, change.After.Park));
        Assert.Equal(5, change.After.Loops[Revisions].Allowed);
        Assert.Equal(2, change.Event.Grant?.Rounds);
    }

    [Fact, Trait("Lane", "unit")]
    public void Accept_on_an_escalation_whose_pending_park_takes_effect_records_no_override()
    {
        var producing = In("produce", new LoopState(2, 3, Last: null));
        var gate = BlockId.From("gate.test.model");
        var escalated = producing with
        {
            Suspension = new Suspension(
                SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Gate, "gate.test.model scored 0.5", "service:desk", Mirrors: null,
                new ResumeAction(ResumeKind.Override, new WithheldTrigger(0, Failed()), gate, Effects: [])),
            PendingPark = BudgetPark,
        };

        var applied = Assert.IsType<Applied>(Decide(escalated, Answer(AnswerVariant.Accept)));

        var change = applied.Changes[0];
        Assert.Equal((EngineStates.Parked, ParkReason.Budget), (change.After.State, change.After.Park));
        Assert.Null(change.Event.Override);
    }

    /// <summary>A budget park, as BudgetRules decides one from a Step report.</summary>
    internal static Suspension BudgetPark { get; } = new(
        SuspensionKind.Park, ParkReason.Budget, ParkKind.Step, Escalation: null, "budget spent", "service:test", Mirrors: null,
        new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []));

    private static void AssertParkedAtCap(Applied applied, WithheldTrigger withheld)
    {
        var after = applied.Changes[0].After;
        Assert.Equal((EngineStates.Parked, ParkReason.Rounds), (after.State, after.Park));
        var top = after.Suspension!; // why: parked, asserted just above
        Assert.Equal((ParkKind.Refusal, ResumeKind.Rerun, withheld), (top.ParkKind!.Value, top.Resume.Kind, top.Resume.Withheld));
        Assert.Equal(("produce", RunToken.For(3, 1)), (after.Frame.State, after.Frame.Run!.Token));
        Assert.Equal(3, after.Loops[Revisions].Round);
        Assert.Empty(applied.FollowUps);
    }

    private static Decision Decide(WorkflowInstance instance, Trigger trigger) =>
        TransitionDecision.Decide(new DecisionInput(instance, StepRecipe, trigger, GateAnswers.None, EngineFixture.Context()));

    private static StepFailed Failed() => new("service:desk", RunToken.For(3, 1), "the build broke", Tokens: 10, new string('e', 64));

    private static AnswerGiven Answer(AnswerVariant variant, string? text = null, int? amount = null) => new(
        "human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64)), variant, text, amount, Forwarded: null);
}
