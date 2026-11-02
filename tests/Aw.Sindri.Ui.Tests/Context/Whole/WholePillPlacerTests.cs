using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>UX pass A-15: the labels on the map's edges never lie on one another; a label that finds no free place is left out.</summary>
[Lane(TestLane.Unit)]
public sealed class WholePillPlacerTests
{
    private static WholePill Pill(string words, double x, double y) => new(new WholeEdgeStyle(Dashed: false, words), x, y);

    private static bool Overlap(WholePill a, WholePill b, double scale)
    {
        var (wa, ha) = WholePillPlacer.SizeOf(a.Style.Pill, scale);
        var (wb, hb) = WholePillPlacer.SizeOf(b.Style.Pill, scale);
        return Math.Abs(a.X - b.X) < (wa + wb) / 2 && Math.Abs(a.Y - b.Y) < (ha + hb) / 2;
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.66)]
    [InlineData(0.3)]
    public void Labels_that_start_on_the_same_point_are_placed_apart(double scale)
    {
        var placed = WholePillPlacer.Place([Pill("decided by", 100, 100), Pill("happens on", 104, 101), Pill("part of", 98, 99)], scale);

        Assert.True(placed.Count >= 2);
        for (var i = 0; i < placed.Count; i++)
        {
            for (var j = i + 1; j < placed.Count; j++)
            {
                Assert.False(Overlap(placed[i], placed[j], scale), $"{placed[i].Style.Pill} lies on {placed[j].Style.Pill}");
            }
        }
    }

    [Fact]
    public void Labels_that_are_far_apart_stay_where_they_were()
    {
        var pills = new[] { Pill("decided by", 100, 100), Pill("part of", 900, 700) };

        var placed = WholePillPlacer.Place(pills, 1.0);

        Assert.Equal(pills, placed);
    }
}
