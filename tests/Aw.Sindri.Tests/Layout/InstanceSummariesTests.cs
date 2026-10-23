using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Layout;

/// <summary>The list row's 'now' sentence is cut to 80 characters, ellipsis inside the 80, never between the halves of a surrogate pair (UI screens E-20).</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceSummariesTests
{
    private const string Prefix = "with you: ";

    private static string Now(string activity) =>
        InstanceSummaries.Of(StatusCases.View("taken-over") with { Activity = activity }, new InstanceSummaryContext(StatusCases.Now)).Now;

    [Fact]
    public void A_cut_never_splits_a_surrogate_pair_so_the_text_stays_valid_unicode()
    {
        var activity = new string('x', 76 - Prefix.Length) + "\U0001F600" + new string('y', 10);   // the emoji's two UTF-16 units sit at 76 and 77, across the cut at 77

        var now = Now(activity);

        Assert.Equal(Prefix + new string('x', 76 - Prefix.Length) + "...", now);
        Assert.True(now.Length <= InstanceSummaries.NowLength);
    }

    [Fact]
    public void A_pair_that_ends_before_the_cut_is_kept()
    {
        var activity = new string('x', 75 - Prefix.Length) + "\U0001F600" + new string('y', 10);   // units at 75 and 76, both before the cut at 77

        Assert.Equal(Prefix + new string('x', 75 - Prefix.Length) + "\U0001F600...", Now(activity));
    }

    [Fact]
    public void The_row_names_who_started_it_when_and_its_root()
    {
        var started = new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);
        var view = StatusCases.View("taken-over") with { Address = InstanceAddress.From("ITM-7/step/1/child.2"), StartedBy = "human:prover", StartedAt = started };

        var row = InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now));

        Assert.Equal("human:prover", row.StartedBy);
        Assert.Equal(started, row.StartedAt);
        Assert.Equal("ITM-7/step/1", row.RootAddress);
    }

    [Fact]
    public void The_row_says_who_the_instance_waits_on()
    {
        var context = new InstanceSummaryContext(StatusCases.Now);
        var agent = StatusCases.View("desk-task") with { Task = null, AgentRun = new OpenRun(RunToken.For(1, 1), StepRuntime.Agent, "LCH-1", Task: null) };

        Assert.Equal("you", InstanceSummaries.Of(StatusCases.View("desk-task"), context).WaitingOn);
        Assert.Equal("agent", InstanceSummaries.Of(agent, context).WaitingOn);
        Assert.Null(InstanceSummaries.Of(StatusCases.View("done"), context).WaitingOn);
    }

    [Fact]
    public void A_root_is_its_own_root_and_an_unrecorded_start_reads_null()
    {
        var row = InstanceSummaries.Of(StatusCases.View("taken-over") with { Address = InstanceAddress.From("ITM-7/step/1") }, new InstanceSummaryContext(StatusCases.Now));

        Assert.Equal("ITM-7/step/1", row.RootAddress);
        Assert.Null(row.StartedBy);
        Assert.Null(row.StartedAt);
        Assert.Null(row.LineStage);
    }

    [Fact]
    public void A_taken_over_instance_row_names_who_took_it_over()
    {
        var taken = InstanceSummaries.Of(StatusCases.View("taken-over"), new InstanceSummaryContext(StatusCases.Now));
        var desk = InstanceSummaries.Of(StatusCases.View("desk-task"), new InstanceSummaryContext(StatusCases.Now));

        Assert.Equal("human:thor", taken.TakenOverBy);
        Assert.Null(desk.TakenOverBy);
    }
}
