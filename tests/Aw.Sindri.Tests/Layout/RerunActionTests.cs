using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Layout;

/// <summary>F5 PR B: 'RerunInstance' is offered only on a root instance that ended failed, unresolved or cancelled, and a rerun carries the address it reruns.</summary>
[Lane(TestLane.Unit)]
public sealed class RerunActionTests
{
    private static readonly ActionId _rerun = ActionId.From("RerunInstance");

    [Theory]
    [InlineData("failed")]
    [InlineData("unresolved")]
    [InlineData("cancelled")]
    public void An_ended_root_instance_that_did_not_succeed_offers_the_rerun_on_its_row_and_its_detail(string name)
    {
        var view = StatusCases.View(name);

        Assert.Contains(_rerun, InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now)).Actions);
        Assert.Contains(_rerun, InstanceViews.Detail(view, Context()).Actions);
    }

    [Theory]
    [InlineData("done")]
    [InlineData("desk-task")]
    [InlineData("parked-budget")]
    [InlineData("taken-over")]
    public void A_finished_or_live_instance_offers_no_rerun(string name)
    {
        var view = StatusCases.View(name);

        Assert.DoesNotContain(_rerun, InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now)).Actions);
        Assert.DoesNotContain(_rerun, InstanceViews.Detail(view, Context()).Actions);
    }

    [Fact]
    public void An_instance_that_ended_already_satisfied_offers_no_rerun()
    {
        var view = StatusCases.View("done") with { Outcome = OutcomeName.From("alreadySatisfied") };

        Assert.Empty(Reruns.Of(view));
    }

    [Fact]
    public void A_child_that_failed_offers_no_rerun()
    {
        var failed = StatusCases.View("failed");

        _ = Assert.Single(Reruns.Of(failed));
        Assert.Empty(Reruns.Of(failed with { ParentId = WorkflowInstanceId.Of(InstanceAddress.From("T-3/step/1")) }));
    }

    [Fact]
    public void A_rerun_carries_the_address_it_reruns_on_its_row_and_its_detail_and_others_carry_none()
    {
        var old = InstanceAddress.From("T-3/step/1");
        var view = StatusCases.View("desk-task");
        var again = InstanceViews.Of(StatusCases.Instance("desk-task"), StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext(Again: old));

        Assert.Null(InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now)).Again);
        Assert.Equal(old, InstanceSummaries.Of(again, new InstanceSummaryContext(StatusCases.Now)).Again);
        Assert.Equal(old, InstanceViews.Detail(again, Context()).Again);
        Assert.Null(InstanceViews.Detail(view, Context()).Again);
    }

    private static InstanceDetailContext Context() => new("t", Parent: null, new ViewCounts(0, 0, 0, Context: null), StatusCases.Now);
}
