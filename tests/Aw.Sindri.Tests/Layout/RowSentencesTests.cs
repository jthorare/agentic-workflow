using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Layout;

/// <summary>The list row's 'now' is its own sentence, one pattern per chip (API spec §3.8 'now'); it is not a cut of 'next'.</summary>
[Lane(TestLane.Unit)]
public sealed class RowSentencesTests
{
    private static readonly InstanceSummaryContext _context = new(StatusCases.Now);

    private static InstanceSummary Row(InstanceView view, InstanceSummaryContext? context = null) => InstanceSummaries.Of(view, context ?? _context);

    private static InstanceView Running(string step)
    {
        var instance = EngineFixture.At(EngineFixture.Step, "produce");
        var view = InstanceViews.Of(instance with { Frame = instance.Frame with { Run = instance.Frame.Run! with { Runtime = StepRuntime.Agent } } }, StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());
        return view with { Step = new StepDeclaration("active", step, step) };
    }

    [Fact]
    public void A_desk_task_row_says_what_it_needs_from_you_as_a_desk_answer()
    {
        var row = Row(StatusCases.View("desk-task"));

        Assert.Equal(("DECISION", "needs your output (desk answer)"), (row.Chip.Word, row.Now));
    }

    [Fact]
    public void A_step_park_needs_your_answer()
    {
        var row = Row(StatusCases.View("parked-needs-answer-step"));

        Assert.Equal(("PARKED", "needs your answer"), (row.Chip.Word, row.Now));
    }

    [Fact]
    public void An_escalation_needs_your_answer()
    {
        var row = Row(StatusCases.View("escalated-gate"));

        Assert.Equal(("ESCALATED", "needs your answer"), (row.Chip.Word, row.Now));
    }

    [Fact]
    public void A_park_that_takes_no_answer_says_what_it_waits_for()
    {
        var row = Row(StatusCases.View("parked-drift"));

        Assert.Equal(("PARKED", "parked (spec-conflict (drift))"), (row.Chip.Word, row.Now));
    }

    [Fact]
    public void An_instance_taken_over_is_with_you_and_names_the_task()
    {
        var row = Row(StatusCases.View("taken-over"));

        Assert.Equal(("WITH YOU", "with you: produce the Step output for T-3 (round 1/3)"), (row.Chip.Word, row.Now));
    }

    [Theory]
    [InlineData("step.produce", "agent producing in T-3")]
    [InlineData("step.roll-back", "agent rolling back in T-3")]
    [InlineData("step.run", "agent running in T-3")]
    [InlineData("step.die", "agent dying in T-3")]
    [InlineData("step.agree", "agent agreeing in T-3")]
    public void A_running_agent_step_is_the_actor_the_verb_and_the_item(string step, string now)
    {
        var row = Row(Running(step));

        Assert.Equal(("RUNNING", now), (row.Chip.Word, row.Now));
    }

    [Fact]
    public void An_instance_that_waits_is_the_engine_waiting_in_its_item()
    {
        var row = Row(Running("step.produce") with { Step = new StepDeclaration("wait", "workflow.step", "one child per packet task") });

        Assert.Equal("engine waiting in T-3", row.Now);
    }

    [Fact]
    public void An_instance_with_no_step_is_the_engine_running_in_its_item() => Assert.Equal("engine running in T-3", Row(Running("step.produce") with { Step = null }).Now);

    [Fact]
    public void A_queued_instance_is_queued_and_a_finished_one_says_its_outcome()
    {
        Assert.Equal("queued", Row(Running("step.produce"), new InstanceSummaryContext(StatusCases.Now, NotStarted: true)).Now);
        Assert.Equal(("DONE", "resolved"), (Row(StatusCases.View("done")).Chip.Word, Row(StatusCases.View("done")).Now));
        Assert.Equal("cancelled", Row(StatusCases.View("cancelled")).Now);
    }

    [Theory]
    [InlineData("failed", "FAILED", ChipTone.Fail)]
    [InlineData("unresolved", "DIDN'T WORK OUT", ChipTone.Neutral)]
    [InlineData("cancelled", "CANCELLED", ChipTone.Neutral)]
    [InlineData("done", "DONE", ChipTone.Neutral)]
    public void A_finished_row_wears_the_chip_of_its_outcome_and_still_says_the_outcome(string name, string word, ChipTone tone)
    {
        var row = Row(StatusCases.View(name));

        Assert.Equal((new StateChip(word, tone), name == "done" ? "resolved" : name), (row.Chip, row.Now));
    }

    [Fact]
    public void A_park_or_escalation_with_no_answer_line_and_no_detail_is_the_bare_word()
    {
        var parked = StatusCases.View("parked-drift") with { Detail = null };

        Assert.Equal("parked", Row(parked).Now);
    }

    [Fact]
    public void An_escalation_with_no_answer_line_says_what_it_waits_for()
    {
        var escalated = StatusCases.View("escalated-gate") with { AnswerLine = null, Detail = "gate failed" };

        Assert.Equal("escalated (gate failed)", Row(escalated).Now);
    }

    [Fact]
    public void A_sentence_over_80_characters_is_cut_to_80_with_the_ellipsis_inside()
    {
        var long_ = StatusCases.View("taken-over") with { Activity = new string('a', 120) };

        var now = Row(long_).Now;

        Assert.Equal(InstanceSummaries.NowLength, now.Length);
        Assert.EndsWith("...", now, StringComparison.Ordinal);
    }
}
