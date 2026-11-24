using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The minimap's gutter (#4149) is at most half the canvas wide, in Fit and in the scroll extent alike (#4191).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphGutterTests
{
    // The proof of the cap: the rows at 400 and 300 fail without it.
    [Theory]
    [InlineData(1000, 232)]   // 200 + 2 x 16
    [InlineData(464, 232)]
    [InlineData(400, 200)]   // half the canvas, not 232 of its 400
    [InlineData(300, 150)]
    public void The_gutter_is_the_map_and_its_margins_but_never_over_half_the_canvas(double canvas, double gutter) =>
        Assert.Equal(gutter, GraphZoom.MapGutterFor(canvas), 3);

    // A guard, not the proof: under 464 px the 290 px cards are one column with or without the cap, so this passes either way (#4214).
    [Fact]
    public void Guard_fit_on_a_400_px_canvas_with_the_map_shown_leaves_the_cards_at_least_half_the_width()
    {
        var fit = GraphFit.Compute(GraphFitTests.Chain(40), 400, 520, 1.0);
        var right = fit.Cards.Values.Max(c => c.X + c.Width) * fit.Zoom;
        var bottom = fit.Cards.Values.Max(c => c.Y + c.Height) * fit.Zoom;

        Assert.True(bottom > 2 * 520, "the fact needs the map shown");
        Assert.True(right >= 400 - GraphZoom.MapGutterFor(400) - 0.5, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the cards end at x {right}, under half of the 400 px canvas"));
    }
}
