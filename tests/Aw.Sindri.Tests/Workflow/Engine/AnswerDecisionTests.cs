using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The answer decision (spec §5.8, W7) over the real rules: fit, grant before the pop, resume under the answer's key.</summary>
public sealed class AnswerDecisionTests
{
    [Fact, Trait("Lane", "unit")]
    public void Grant_on_a_no_progress_park_performs_the_withheld_loop_transition()
    {
        var parked = NoProgressPark();

        var applied = Assert.IsType<Applied>(Decide(parked, Answer(AnswerVariant.Grant, amount: 1)));

        var after = applied.Changes[0].After;
        Assert.Equal("produce", after.State);
        Assert.Null(after.Suspension);
        Assert.Equal(3, after.Loops["revisions"].Round);
        Assert.Equal("answer", applied.Changes[0].Event.Trigger.Kind);
        Assert.Equal(1, applied.Changes[0].Event.Grant?.Rounds);
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_on_a_no_progress_park_resumes_and_records_the_answer()
    {
        var parked = NoProgressPark();

        var applied = Assert.IsType<Applied>(Decide(parked, Answer(AnswerVariant.Text, text: "Read the version from the assembly.")));

        var after = applied.Changes[0].After;
        Assert.Equal("produce", after.State);
        Assert.True(after.Loops["revisions"].TextAnswered);
        Assert.Contains(after.Facts.Keys, k => k.Value == "fact:answer");
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_answers_a_no_progress_park_again_after_an_earlier_text_answer()
    {
        var parked = NoProgressPark();
        var answered = parked with
        {
            Loops = new Dictionary<string, LoopState>(parked.Loops, StringComparer.Ordinal) { ["revisions"] = parked.Loops["revisions"] with { TextAnswered = true } },
        };

        var applied = Assert.IsType<Applied>(Decide(answered, Answer(AnswerVariant.Text, text: "again")));

        Assert.Equal("produce", applied.Changes[0].After.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_answer_that_does_not_fit_is_rejected()
    {
        var rejected = Assert.IsType<Rejected>(Decide(NoProgressPark(), Answer(AnswerVariant.Accept)));

        Assert.Equal(WorkflowErrors.TriggerNotAllowed.Code, rejected.Error.Code);
    }

    private static readonly string[] _runtimes = ["step.produce", "step.checks", "step.verify"];

    private static Decision Decide(WorkflowInstance instance, Trigger trigger) =>
        TransitionDecision.Decide(new DecisionInput(instance, Step, trigger, GateAnswers.None, EngineFixture.Context(_runtimes)));

    /// <summary>produce, checks, produce, checks with the same findings twice: parked with no-progress.</summary>
    private static WorkflowInstance NoProgressPark()
    {
        var instance = At(Step, "produce");
        (int Entry, string Outcome, (string, string)[] Outputs)[] reports =
        [
            (1, "done", [("fact:output", "one")]),
            (2, "failed", [("fact:findings", "same")]),
            (3, "done", [("fact:output", "two")]),
            (4, "failed", [("fact:findings", "same")]),
        ];
        foreach (var (entry, outcome, outputs) in reports)
        {
            instance = Assert.IsType<Applied>(Decide(instance, Completed(entry, outcome, outputs) with { Tokens = 0 })).Changes[0].After;
        }

        Assert.Equal(ParkReason.NoProgress, instance.Park);
        return instance;
    }

    private static AnswerGiven Answer(AnswerVariant variant, string? text = null, int? amount = null) => new(
        "human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64)), variant, text, amount, Forwarded: null);
}
