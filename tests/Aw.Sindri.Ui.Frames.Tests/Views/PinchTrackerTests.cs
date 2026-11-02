using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The pinch and trackpad-magnify math the zoom stage uses.</summary>
[Lane(TestLane.Unit)]
public sealed class PinchTrackerTests
{
    [Fact]
    public void Each_step_is_the_ratio_to_the_previous_scale()
    {
        var pinch = new PinchTracker();
        Assert.Equal(1.5, pinch.Step(1.5), 9);
        Assert.Equal(2.0 / 1.5, pinch.Step(2.0), 9);
        Assert.Equal(0.5, pinch.Step(1.0), 9);
    }

    [Fact]
    public void Ending_a_pinch_starts_the_next_one_from_one()
    {
        var pinch = new PinchTracker();
        _ = pinch.Step(3);
        pinch.End();
        Assert.Equal(1.2, pinch.Step(1.2), 9);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void An_unusable_scale_changes_nothing(double scale)
    {
        var pinch = new PinchTracker();
        _ = pinch.Step(2);
        Assert.Equal(1, pinch.Step(scale), 9);
        Assert.Equal(1.5, pinch.Step(3), 9);
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(0.25, 1.25)]
    [InlineData(-0.5, 0.5)]
    [InlineData(-5.0, 0.05)]
    public void A_magnify_increment_is_one_plus_itself_held_above_zero(double magnification, double expected) =>
        Assert.Equal(expected, PinchTracker.MagnifyFactor(magnification), 9);

    [Fact]
    public void Pinching_zooms_around_the_origin_inside_the_range()
    {
        var origin = new Avalonia.Point(300, 200);
        var view = ZoomView.Identity.ZoomAt(origin, 0.8);
        var world = ZoomView.Identity.ToWorld(origin);
        Assert.Equal(0.8, view.Scale, 9);
        Assert.Equal(world.X, view.ToWorld(origin).X, 6);
        Assert.Equal(ZoomRange.Default.Min, view.ZoomAt(origin, 0.01).Scale, 9);
    }
}
