using Aw.Judge.Infrastructure.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>
/// Jev's image limits are inclusive: 280 to 1568 px a side, 78,400 to 2,458,624 pixels. 280 x 280 is exactly the smallest pixel count and
/// 1568 x 1568 exactly the largest, so no image can break a pixel limit without breaking a side limit first; the neighbours pin both edges.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class JevImageLimitsTests
{
    [Theory]
    [InlineData(280, 280, true)]      // the smallest side and exactly 78,400 pixels
    [InlineData(279, 281, false)]     // a side one under, and 78,399 pixels
    [InlineData(280, 1568, true)]     // the longest side
    [InlineData(1568, 1568, true)]    // the largest side and exactly 2,458,624 pixels
    [InlineData(1569, 280, false)]    // a side one over
    [InlineData(1568, 279, false)]    // a side one under, the other at its largest
    public void An_image_fits_jev_exactly_at_its_bounds(int width, int height, bool fits) =>
        Assert.Equal(fits, JevImageLimits.Fits(JevImages.Attachment("/ev/home.png", width, height)));
}
