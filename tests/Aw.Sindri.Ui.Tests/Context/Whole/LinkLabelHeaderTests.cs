using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#4442: a link label never covers a layer's heading row (the lane strip with "Product" in it), at any zoom, and never a node.</summary>
[Lane(TestLane.Unit)]
public sealed class LinkLabelHeaderTests
{
    [Theory]
    [InlineData(0.64)]
    [InlineData(1.08)]
    public void A_link_label_never_covers_a_band_heading(double scale)
    {
        var geometry = WholeMapLayout.Place(WholeMapLayoutTests.Draft("nodes-small"));
        var drawn = WholeEdgeSelection.Visible(geometry.Edges, geometry.Nodes, focus: null);
        var headings = HeadingRows(geometry);

        var pills = WholePillLayout.Place(geometry, drawn, focus: null, scale, words => WholePillPlacer.SizeOf(words, scale));

        Assert.NotEmpty(pills);   // the fixture draws labels, so the check below is not vacuous
        foreach (var pill in pills)
        {
            var (width, height) = WholePillPlacer.SizeOf(pill.Style.Pill, scale);
            var rect = new WholeRect(pill.X - (width / 2), pill.Y - (height / 2), width, height);
            foreach (var (title, heading) in headings)
            {
                Assert.False(rect.Intersects(heading), $"the \"{pill.Style.Pill}\" label at {Show(rect)} covers the {title} heading at {Show(heading)} (zoom {scale.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
            }

            Assert.DoesNotContain(geometry.Nodes, n => rect.Intersects(n.Rect));
        }
    }

    // The heading row of each layer: the strip above the band's first row (the lane title sits in it), and the headings of an opened area's canvas.
    private static List<(string Title, WholeRect Rect)> HeadingRows(WholeMapGeometry geometry) =>
    [
        .. geometry.Bands.Select(b => (NodeBands.Title(b.Band), new WholeRect(0, b.LaneTop, geometry.World.Width, WholeMapLayout.LaneLabel))),
        .. (geometry.AreaBands ?? []).Where(b => !b.Sub).Select(b => (b.Title, b.Rect)),
    ];

    private static string Show(WholeRect r) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"({r.X:0.#}, {r.Y:0.#}, {r.Width:0.#} x {r.Height:0.#})");
}
