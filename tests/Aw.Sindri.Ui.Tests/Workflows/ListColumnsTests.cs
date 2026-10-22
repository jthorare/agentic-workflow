using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>F3 workflows spec §2 "List": the columns the list keeps at each width. Under 960 Stage and Waiting on go; under 560 Step, Updated and the reference go too.</summary>
[Lane(TestLane.Unit)]
public sealed class ListColumnsTests
{
    private static (bool Stage, bool Status, bool Step, bool WaitingOn, bool Updated, bool Reference) Kept(double width)
    {
        var columns = ListColumns.For(width);
        return (columns.Shows(ListColumns.Stage), columns.Shows(ListColumns.Status), columns.Shows(ListColumns.Step), columns.Shows(ListColumns.WaitingOn), columns.Shows(ListColumns.Updated), columns.ShowsReference);
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(960)]
    public void At_960_and_wider_all_six_columns_and_the_reference_show(double width) =>
        Assert.Equal((true, true, true, true, true, true), Kept(width));

    [Theory]
    [InlineData(959)]
    [InlineData(560)]
    public void From_559_up_to_960_Stage_and_Waiting_on_are_hidden(double width) =>
        Assert.Equal((false, true, true, false, true, true), Kept(width));

    [Fact]
    public void Under_560_only_Item_and_Status_stay() =>
        Assert.Equal((false, true, false, false, false, false), Kept(559));

    [Theory]
    [InlineData(1440)]
    [InlineData(959)]
    [InlineData(559)]
    public void Item_always_shows_and_every_band_sizes_all_six_columns(double width)
    {
        var columns = ListColumns.For(width);

        Assert.True(columns.Shows(ListColumns.Item));
        Assert.Equal(6, columns.Sizes.Count);
        Assert.All(Enumerable.Range(0, 6), i => Assert.Equal(columns.Shows(i), columns.Sizes[i].Value > 0));   // a hidden column keeps its place at width 0
    }

    [Fact]
    public void The_full_band_is_the_specs_widths()
    {
        var sizes = ListColumns.For(1440).Sizes;

        Assert.Equal([(2.2, true, 200), (128, false, 0), (148, false, 0), (1.8, true, 150), (124, false, 0), (80, false, 0)], sizes.Select(s => (s.Value, s.IsStar, s.Min)));
    }

    [Fact]
    public void A_width_not_measured_yet_keeps_the_full_band() =>
        Assert.Same(ListColumns.For(1440), ListColumns.For(0));
}
