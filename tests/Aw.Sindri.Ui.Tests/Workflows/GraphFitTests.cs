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
        new([.. Enumerable.Range(0, count).Select(i => (IReadOnlyList<string>)[string.Create(System.Globalization.CultureInfo.InvariantCulture, $"step_{i}")])], []);

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

    // The frames of #3684 (measure.txt at 3337c10a18): the release pipeline of 16 steps and its 40-step long form. Layers follow the server's longest-path rule.
    private static GraphFitInput Release(int tail)
    {
        List<IReadOnlyList<string>> layers = [["start"], ["step_1"], ["step_2"], ["step_3"], ["step_4"], ["step_5"], ["gate"], ["big_path", "small_path"]];
        List<(string, string)> edges = [("start", "step_1"), ("step_1", "step_2"), ("step_2", "step_3"), ("step_3", "step_4"), ("step_4", "step_5"), ("step_5", "gate"), ("gate", "big_path"), ("gate", "small_path")];
        var steps = tail == 0 ? new[] { "step_6", "step_7", "step_8", "step_9", "step_10", "step_11", "step_12" } : new[] { "step_6", "step_7", "step_8", "step_9", "step_10", "step_12" };
        var previous = "big_path";
        foreach (var id in steps)
        {
            layers.Add([id]);
            edges.Add((previous, id));
            previous = id;
        }

        if (tail > 0)
        {
            layers.Add(["gate_2"]);
            layers.Add(["diff", "same"]);
            layers.Add(["post"]);
            edges.AddRange([(previous, "gate_2"), ("gate_2", "diff"), ("gate_2", "same"), ("diff", "post")]);
            previous = "post";
            foreach (var n in Enumerable.Range(1, tail))
            {
                var id = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"t_{n}");
                layers.Add([id]);
                edges.Add((previous, id));
                previous = id;
            }
        }

        return new GraphFitInput(layers, edges);
    }

    [Fact]
    public void Fit_keeps_a_forks_arms_in_the_forks_row_at_every_width()
    {
        for (var width = 900; width <= 1500; width += 20)
        {
            var fit = GraphFit.Compute(Release(0), width, CanvasHeight, 1.0);
            var gate = fit.Cards["gate"];

            Assert.True(fit.Cards["big_path"].X > gate.X && fit.Cards["small_path"].X > gate.X, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"width {width}: an arm of the gate wrapped to the next row"));
        }
    }

    [Fact]
    public void Fit_of_the_16_step_release_is_98_percent_in_4_columns_and_4_rows()
    {
        var fit = GraphFit.Compute(Release(0), CanvasWidth, CanvasHeight, 1.0);

        Assert.Equal(0.98, fit.Zoom, 3);
        Assert.Equal(4, fit.Columns);
        Assert.Equal(4, fit.Rows);
    }

    [Fact]
    public void Fit_wraps_at_its_own_zoom_when_the_floor_binds()
    {
        // At 1000 px the 16 steps wrap sooner: 3 columns, Fit 89 % (wrapped at Fit's own zoom, not at 100 %, which would give 2).
        var fit = GraphFit.Compute(Release(0), 1000, 628, 1.0);

        Assert.Equal(3, fit.Columns);
        Assert.Equal(0.89, fit.Zoom, 3);
    }

    [Fact]
    public void Fit_of_the_16_step_release_at_130_percent_text_is_83_percent()
    {
        Assert.Equal(0.83, GraphFit.Compute(Release(0), CanvasWidth, CanvasHeight, 1.3).Zoom, 3);
    }

    [Fact]
    public void Fit_never_scrolls_sideways_when_the_floor_binds()
    {
        // canvas-40: 4 columns, stops at the 86 % floor, and the world is no wider than the canvas (hScroll 0).
        var fit = GraphFit.Compute(Release(21), CanvasWidth, CanvasHeight, 1.0);

        Assert.Equal(0.86, fit.Zoom, 3);
        Assert.Equal(4, fit.Columns);
        Assert.True(fit.Width * fit.Zoom <= CanvasWidth + 0.5);
    }
}
