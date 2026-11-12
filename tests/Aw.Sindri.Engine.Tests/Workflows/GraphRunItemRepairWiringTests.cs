using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The finish of a run asks the run-item repair about that run (issue 2588), and a failing repair does not change how the run ends.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphRunItemRepairWiringTests
{
    [Fact]
    public async Task A_run_that_ends_is_handed_to_the_repair()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.ItemRepair.Delay = TimeSpan.FromMilliseconds(500);   // the repair lands after the run's status is final (#2934)
        var workflow = await rig.Define(GraphRig.Spec("unlimited", [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo")], [GraphRig.Edge("start", "a")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal([run.Id.Value], rig.ItemRepair.Runs);
    }

    [Fact]
    public async Task A_repair_that_fails_does_not_fail_the_run()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.ItemRepair.Delay = TimeSpan.FromMilliseconds(500);   // the repair lands after the run's status is final (#2934)
        rig.ItemRepair.Fail = SindriErrors.LedgerUnavailable.With("ledger.db: database or disk is full");
        var workflow = await rig.Define(GraphRig.Spec("unlimited", [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo")], [GraphRig.Edge("start", "a")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        _ = Assert.Single(rig.ItemRepair.Runs);   // asked once: the finish was not retried
    }
}
