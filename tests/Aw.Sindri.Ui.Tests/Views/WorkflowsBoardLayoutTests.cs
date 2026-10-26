using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Workflows;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The Workflows board's widths (F3 workflows spec §2, §7): the line's six lanes and Not on a line (the golden holds runs of other workflows), each no narrower than Size.LaneMin (Done, the last, Size.LaneMinLast), the lane row no narrower than
/// Size.BoardMin so the board scrolls inside its host as the window narrows, and the run drawer at 48 % of the window clamped to 360 to 480.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsBoardLayoutTests : FrameHost
{
    private const double LaneMin = 200;
    private const double LaneMinLast = 150;
    private const double BoardMin = 1230;

    private async Task<(Window Window, ShellViewModel Shell)> OnBoard(double width)
    {
        Server.InstancesRewrite = LineGolden.AsLine;
        var (window, shell) = await Connected(s => s.Workflows.Lanes.Sum(l => l.Cards.Count) > 0, width, 900);
        shell.GoTo(PageId.Workflows);
        Settle(window);
        return (window, shell);
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_lanes_keep_their_minimum_widths_and_the_lane_row_its_minimum(double width) => OnUi(async () =>
    {
        var (window, _) = await OnBoard(width);

        var grid = Assert.Single(window.GetVisualDescendants().OfType<LaneGrid>());
        Assert.Equal(7, grid.Children.Count);
        var widths = grid.Children.Select(c => c.Bounds.Width).ToList();
        Assert.All(widths.Take(6), w => Assert.True(w >= LaneMin - 0.5, "a lane is narrower than Size.LaneMin"));
        Assert.True(widths[^1] >= LaneMinLast - 0.5, "the Done lane is narrower than Size.LaneMinLast");
        Assert.True(widths[^1] < widths[0]);   // Done is the narrower lane (star 0.6)
        Assert.True(Named<ItemsControl>(window, "WorkflowsBoardLanes").Bounds.Width >= BoardMin - 0.5);
        Assert.True(Named<ScrollViewer>(window, "WorkflowsBoardScroll").Bounds.Width <= width + 0.5);   // the page itself never scrolls sideways
    });

    [Theory]
    [InlineData(1440, 480)]
    [InlineData(1000, 480)]
    [InlineData(900, 432)]
    public Task The_drawer_is_48_percent_of_the_window_up_to_480(double width, double drawer) => OnUi(async () =>
    {
        var (window, shell) = await OnBoard(width);

        Assert.True(shell.Workflows.OpenInspector(shell.Workflows.Lanes.SelectMany(l => l.Cards).First().Address));
        Settle(window);

        var host = Named<Border>(window, "WorkflowsInspectorHost");
        var view = Assert.IsType<RunInspectorView>(host.Child);
        Assert.Equal(drawer, view.Width, 0.5);
        Assert.True(Visible(window, "WorkflowsInspectorHost"));
    });

    [Theory]
    [InlineData(1440, 480)]
    [InlineData(1000, 480)]
    [InlineData(900, 432)]
    [InlineData(700, 360)]
    [InlineData(300, 360)]
    public void The_drawer_width_is_clamped_between_360_and_480(double window, double expected) => Assert.Equal(expected, InspectorWidth.For(window), 0.5);
}
