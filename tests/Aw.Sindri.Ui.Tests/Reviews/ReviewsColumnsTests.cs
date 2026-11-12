using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>The Reviews table's columns (#3100): a table too narrow for them (a sheet open beside the page) keeps the first three facts and drops the right-hand ones, never overlapping.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewsColumnsTests
{
    [Theory]
    [InlineData(1200, new[] { 140.0, 70, 120, 160, 140 })]
    [InlineData(789, new[] { 140.0, 70, 120, 160, 0 })]
    [InlineData(629, new[] { 140.0, 70, 120, 0, 0 })]
    [InlineData(449, new[] { 140.0, 70, 0, 0, 0 })]
    [InlineData(0, new[] { 140.0, 70, 120, 160, 140 })]
    public void Columns_drop_from_the_right_until_the_title_keeps_160(double width, double[] expected) =>
        Assert.Equal(expected, PageLayout.ReviewWidths(width).Skip(1).ToArray());

    [Theory]
    [InlineData(966)]
    [InlineData(1200)]
    public void With_a_sheet_open_only_the_first_three_facts_and_the_age_stay_however_wide_the_page(double width) =>
        Assert.Equal([140.0, 70, 120, 160, 0], [.. PageLayout.ReviewWidths(width, peek: true).Skip(1)]);

    [Theory]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(700)]
    [InlineData(900)]
    public void The_kept_columns_always_leave_the_title_160(double width) =>
        Assert.True(160 + PageLayout.ReviewWidths(width).Sum() <= width);
}
