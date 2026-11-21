using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The pure Fit of the workflow canvas (#3684): the zoom floor is a font size, and a small graph is never blown up.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphFitTests
{
    internal const double CanvasWidth = 1440;
    internal const double CanvasHeight = 728;   // 1440 x 900 window: the 56 px zoom band is under this clip

    /// <summary>A chain of single-step layers: step_0 to step_{count-1}.</summary>
    internal static GraphFitInput Chain(int count) =>
        new([.. Enumerable.Range(0, count).Select(i => (IReadOnlyList<string>)[string.Create(System.Globalization.CultureInfo.InvariantCulture, $"step_{i}")])], new HashSet<string>(StringComparer.Ordinal));

    [Theory]
    [InlineData(1.0, 0.86)]
    [InlineData(1.3, 0.66)]
    public void Fit_floor_is_12_px_card_text(double textScale, double floor)
    {
        // 12 / 14 = 86 % at 100 % text and 12 / 18.2 = 66 % at 130 %; 40 steps do not fit, so Fit stops at the floor.
        Assert.Equal(floor, GraphFit.Floor(textScale), 3);
        Assert.Equal(floor, GraphFit.Compute(Chain(40), CanvasWidth, CanvasHeight, textScale).Zoom, 3);
    }

    [Fact]
    public void Fit_never_exceeds_100_percent()
    {
        var fit = GraphFit.Compute(Chain(4), CanvasWidth, CanvasHeight, 1.0);

        Assert.Equal(1.0, fit.Zoom, 3);
        Assert.Equal(4, fit.Columns);
        Assert.Equal(1, fit.Rows);
    }
}
