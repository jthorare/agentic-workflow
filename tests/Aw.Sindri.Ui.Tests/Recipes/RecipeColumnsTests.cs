using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Layout;
using Aw.Sindri.Ui.Views.Recipes;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The Recipes table's columns by page width (recipes spec §2, narrow layout): the header and the rows read the same widths, and what a sheet beside the page covers is dropped in a fixed order.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeColumnsTests
{
    [Theory]
    [InlineData(PageTier.Wide, new[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [InlineData(PageTier.Mid, new[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [InlineData(PageTier.Narrow, new[] { 2, 5, 6, 7, 8 })]
    [InlineData(PageTier.Tight, new[] { 2, 5 })]
    internal void Each_tier_keeps_its_columns(PageTier tier, int[] kept)
    {
        var shown = RecipeColumns.Widths(tier).Select((w, i) => (w, i)).Where(c => c.i > 0 && c.w > 0).Select(c => c.i);

        Assert.Equal(kept, shown);
    }

    [Fact]
    public void Every_tier_has_the_nine_columns_and_the_workflow_keeps_its_star()
    {
        foreach (var tier in Enum.GetValues<PageTier>())
        {
            var widths = RecipeColumns.Widths(tier);

            Assert.Equal((9, 0d), (widths.Count, widths[0]));
        }
    }

    [Fact]
    public void Standing_and_fingerprint_stay_at_every_tier()
    {
        foreach (var tier in Enum.GetValues<PageTier>())
        {
            Assert.All([2, 5], c => Assert.True(RecipeColumns.Widths(tier)[c] > 0));
        }
    }
}
