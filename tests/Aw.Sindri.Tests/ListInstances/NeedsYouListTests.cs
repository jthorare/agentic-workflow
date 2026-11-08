using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>'workflow list --needs-me' (engine spec section 6.3, README X5): a parent mirroring a listed child shows once, under the child. The Decisions and Tasks grouping is the CLI's, pinned in ReadVerbsClientTests.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouListTests
{
    [Fact]
    public void A_parent_mirroring_a_listed_child_is_shown_once_under_the_child()
    {
        var parent = StatusCases.View("parked-no-progress-child");
        var child = StatusCases.View("parked-rounds") with { Address = InstanceAddress.From("T-3/step/1/step.4") };

        var shown = NeedsYouList.Shown([parent, child]);

        Assert.Equal(["T-3/step/1/step.4"], shown.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_parent_whose_child_is_not_listed_stays_and_the_order_is_kept()
    {
        var parent = StatusCases.View("parked-no-progress-child");
        var other = StatusCases.View("desk-task") with { Address = InstanceAddress.From("T-9/step/1") };

        var shown = NeedsYouList.Shown([other, parent]);

        Assert.Equal([other.Address.Value, parent.Address.Value], shown.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void Nothing_needs_you_is_the_empty_state() => Assert.Equal("nothing needs you", NeedsYouList.Nothing);
}
