using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>UX pass C-5, C-7 and A-15: one zoom model. A small map may be fitted up to twice its size, the map can always be zoomed out to its fit (or a quarter), and a zoom button that cannot act is off.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeZoomLimitsTests
{
    [Theory]
    [InlineData(5.0, 0.25, 2.0)]
    [InlineData(1.2, 0.25, 2.0)]
    [InlineData(0.6, 0.25, 2.0)]
    [InlineData(0.1, 0.1, 2.0)]
    [InlineData(0.001, 0.02, 2.0)]
    public void The_range_reaches_the_fit_or_a_quarter_and_up_to_twice(double fit, double min, double max) =>
        Assert.Equal((min, max), WholeMapViewModel.ZoomLimits(fit));

    [Fact]
    public void A_map_that_fits_with_room_to_spare_is_fitted_up_to_twice_its_size() =>
        Assert.Equal(2.0, WholeMapViewModel.FirstOpenScale(3.0));

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(1.9995, false)]
    [InlineData(2.0, false)]
    public void Zoom_in_is_on_until_the_largest_scale(double scale, bool expected) =>
        Assert.Equal(expected, WholeMapViewModel.CanZoomIn(scale, max: 2.0));

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(0.2502, false)]
    [InlineData(0.25, false)]
    public void Zoom_out_is_on_until_the_smallest_scale(double scale, bool expected) =>
        Assert.Equal(expected, WholeMapViewModel.CanZoomOut(scale, min: 0.25));
}
