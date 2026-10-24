using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Desktop;

public sealed class ScreenComparisonTests
{
    private const uint White = 0xFFFFFFFF;
    private const uint Black = 0x000000FF;

    private static RgbaImage Frame(int width, int height, Func<int, int, bool>? black = null) =>
        TestImages.Draw(width, height, (x, y) => black?.Invoke(x, y) == true ? Black : White);

    [Fact, Trait("Lane", "unit")]
    public void Identical_frames_differ_by_nothing_and_pass_any_limit()
    {
        var compared = ScreenComparison.Compare(Frame(20, 10), Frame(20, 10), []).Value;

        Assert.Equal((0L, 0d, null), (compared.Pixels.DiffPixels, compared.Pixels.DiffPercent, compared.Problem));
        Assert.True(compared.Within(0));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_block_of_changed_pixels_is_counted_as_a_share_of_the_whole_frame()
    {
        var changed = Frame(20, 10, static (x, y) => x is >= 5 and < 10 && y is >= 3 and < 7);

        var compared = ScreenComparison.Compare(Frame(20, 10), changed, []).Value;

        Assert.Equal((20L, 10d), (compared.Pixels.DiffPixels, compared.Pixels.DiffPercent));
        Assert.True(compared.Within(10));
        Assert.False(compared.Within(9.99));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_masked_region_is_painted_over_in_both_frames_so_it_never_counts()
    {
        var changed = Frame(20, 10, static (x, y) => x is >= 5 and < 10 && y is >= 3 and < 7);

        var compared = ScreenComparison.Compare(Frame(20, 10), changed, [new Region(5, 3, 5, 4)]).Value;

        Assert.Equal(0L, compared.Pixels.DiffPixels);
        Assert.Equal(compared.App.PixelAt(0, 0), compared.Design.PixelAt(0, 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_mask_that_runs_past_the_frame_is_clipped_to_it()
    {
        var compared = ScreenComparison.Compare(Frame(20, 10), Frame(20, 10, static (x, _) => x >= 18), [new Region(18, 0, 50, 50)]).Value;

        Assert.Equal(0L, compared.Pixels.DiffPixels);
    }

    [Fact, Trait("Lane", "unit")]
    public void Frames_of_two_sizes_are_compared_where_they_overlap_and_never_pass()
    {
        var compared = ScreenComparison.Compare(Frame(20, 10), Frame(30, 12), []).Value;

        Assert.Equal(0L, compared.Pixels.DiffPixels);
        Assert.Equal("the frames differ in size: app 20x10, design 30x12", compared.Problem);
        Assert.False(compared.Within(100));
    }
}
