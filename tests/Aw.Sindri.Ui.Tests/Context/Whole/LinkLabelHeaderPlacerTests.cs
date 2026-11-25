using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#4442: a heading strip is an obstacle for a link label, like a node.</summary>
[Lane(TestLane.Unit)]
public sealed class LinkLabelHeaderPlacerTests
{
    [Fact]
    public void A_heading_that_fills_the_gap_leaves_the_label_out()
    {
        var top = new WholeRect(0, 0, 100, 28);
        var bottom = new WholeRect(0, 100, 100, 28);
        var heading = new WholeRect(0, 40, 400, 48);

        _ = Assert.NotNull(LinkLabelPlacer.Place(top, bottom, width: 60, height: 14, [top, bottom]));
        Assert.Null(LinkLabelPlacer.Place(top, bottom, width: 60, height: 14, [top, bottom, heading]));
    }
}
