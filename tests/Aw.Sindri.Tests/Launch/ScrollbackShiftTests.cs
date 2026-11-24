using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// The content match that holds a scrolled-back view once tmux history is full (issue 3829): the previous view's rows looked up in a capture with extra rows
/// above the view. Each capture below is the extra rows first, then the view.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ScrollbackShiftTests
{
    [Fact]
    public void Rows_that_did_not_move_are_shift_zero() =>
        Expect.Equal(0, ScrollbackShift.Find(["d", "e", "f"], ["a", "b", "c", "d", "e", "f"], extra: 3));

    [Fact]
    public void Rows_that_moved_up_are_the_shift_of_the_rows_that_arrived() =>
        Expect.Equal(2, ScrollbackShift.Find(["b", "c", "d"], ["a", "b", "c", "d", "e", "f"], extra: 3));

    [Fact]
    public void Rows_at_the_top_of_the_capture_are_the_largest_shift() =>
        Expect.Equal(2, ScrollbackShift.Find(["a", "b"], ["a", "b", "c", "d"], extra: 2));

    [Fact]
    public void Repeated_rows_take_the_smallest_shift() =>
        Expect.Equal(0, ScrollbackShift.Find(["x", "x"], ["x", "x", "x", "x"], extra: 2));

    [Fact]
    public void Rows_found_at_no_shift_are_no_match() =>
        Expect.Equal(expected: null, ScrollbackShift.Find(["p", "q"], ["a", "b", "c", "d"], extra: 2));

    [Fact]
    public void A_blank_view_is_shift_zero() =>
        Expect.Equal(0, ScrollbackShift.Find(["", "  "], ["a", "b", "c", "d"], extra: 2));

    [Fact]
    public void Without_extra_rows_only_shift_zero_is_tried()
    {
        Expect.Equal(0, ScrollbackShift.Find(["a", "b"], ["a", "b"], extra: 0));
        Expect.Equal(expected: null, ScrollbackShift.Find(["a", "b"], ["b", "c"], extra: 0));
    }

    [Theory]
    [InlineData(2000, 2000, true)]
    [InlineData(1800, 2000, true)]
    [InlineData(1799, 2000, false)]
    [InlineData(45, 50, true)]
    [InlineData(44, 50, false)]
    [InlineData(8, 9, true)]
    [InlineData(7, 9, false)]
    [InlineData(0, 0, false)]
    public void The_limit_is_the_band_tmux_trims_in(int hist, int limit, bool atLimit) =>
        Expect.Equal(atLimit, ScrollbackShift.AtLimit(hist, limit));
}
