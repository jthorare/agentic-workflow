using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>Which live instances a cancelled line's cancel reaches (LN-WF1): the ones linked to it, and no other.</summary>
public sealed class LinkedCancelTests
{
    private static readonly InstanceAddress _line = InstanceAddress.From("T-3/line/1");

    [Fact, Trait("Lane", "unit")]
    public void The_cancel_reaches_the_lines_live_phases_and_not_the_line_a_child_or_another_lines_phase()
    {
        InstanceRow[] live =
        [
            Row("T-3/line/1"),
            Row("T-3/line/1/phase.p1.1"),
            Row("T-3/line/1/phase.p2.2"),
            Row("T-3/line/1/scoping.2"),
            Row("T-3/line/1/phase.p1.1/build.4"),
            Row("T-3/line/2/phase.p1.1"),
        ];

        Assert.Equal(["T-3/line/1/phase.p1.1", "T-3/line/1/phase.p2.2"], LinkedActivities.LinkedTo(_line, live));
    }

    private static InstanceRow Row(string address) => new(address, "running", NeedsYou: false);
}
