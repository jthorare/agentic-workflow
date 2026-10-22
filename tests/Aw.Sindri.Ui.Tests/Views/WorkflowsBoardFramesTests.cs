using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Workflows;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// F3 workflows slice 2 key frames (UI spec §18.2: one per state, integration lane) for the board: the six lanes with their cards and
/// counts at 1440 and 900 (the board scrolls inside its host, the page does not), the filter with no match, and no runs at all.
/// Saved under frames/ as f3-workflows-board-*.png.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsBoardFramesTests : FrameHost
{
    private const string Prefix = "f3-";

    private async Task<(Window Window, ShellViewModel Shell)> OnBoard(double width, Func<ShellViewModel, bool> ready)
    {
        var (window, shell) = await Connected(ready, width, 900);
        shell.GoTo(PageId.Workflows);
        Settle(window);
        return (window, shell);
    }

    private static bool Drawn(ShellViewModel shell) => shell.Workflows.Lanes.Sum(l => l.Cards.Count) > 0;

    private static List<RunCard> Cards(Window window) => [.. window.GetVisualDescendants().OfType<RunCard>().Where(c => c.IsEffectivelyVisible)];

    private void Lines() => Server.InstancesRewrite = LineGolden.AsLine;

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_board_draws_six_lanes_with_the_runs_in_their_lanes(int width) => OnUi(async () =>
    {
        Lines();
        var (window, shell) = await OnBoard(width, Drawn);

        var grid = Assert.Single(window.GetVisualDescendants().OfType<LaneGrid>());
        Assert.Equal(6, grid.Children.Count);
        Expect.Sequence(["Scoping", "Design", "Plan", "Build and prove", "Merge", "Done"], shell.Workflows.Lanes.Select(l => l.Name));
        Expect.Sequence(shell.Workflows.Lanes.SelectMany(l => l.Cards).Select(c => c.Address), Cards(window).Select(c => c.Address));
        Assert.Equal(shell.Workflows.Lanes.Sum(l => l.Cards.Count), Cards(window).Count);
        Assert.True(Visible(window, "WorkflowsBoard"));
        Assert.False(Visible(window, "WorkflowsPanel"));
        Assert.Equal(shell.Workflows.FilterAllText, Named<ListBoxItem>(window, "WorkflowsFilterAllItem").GetVisualDescendants().OfType<TextBlock>().Single().Text);
        var scroll = Named<ScrollViewer>(window, "WorkflowsBoardScroll");
        Assert.True(scroll.Bounds.Width <= width);   // the page never scrolls sideways
        Assert.True(width > 900 || scroll.Extent.Width > scroll.Viewport.Width + 1);   // at the 900 minimum the board scrolls inside its host
        Frame(window, "workflows-board-" + width.ToString(System.Globalization.CultureInfo.InvariantCulture), Prefix);
    });

    [Fact]
    public Task A_filter_with_no_match_says_so_in_every_lane() => OnUi(async () =>
    {
        Lines();
        var (window, shell) = await OnBoard(1440, Drawn);

        shell.Workflows.SetSearch("no such run");
        Settle(window);

        Assert.True(Visible(window, "WorkflowsNoRunsMatch"));
        Assert.Empty(Cards(window));
        var words = window.GetVisualDescendants().OfType<TextBlock>().Count(t => t.IsEffectivelyVisible && string.Equals(t.Text, Labels.WorkflowsNoRunsMatch, StringComparison.Ordinal));
        Assert.Equal(6, words);
        Frame(window, "workflows-board-no-match", Prefix);
    });

    [Fact]
    public Task No_runs_at_all_says_so_in_place_of_the_board() => OnUi(async () =>
    {
        Server.WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty");
        var (window, _) = await OnBoard(1440, s => s.Workflows.IsEmpty);

        Assert.Equal((true, false), (Visible(window, "WorkflowsBoardEmpty"), Visible(window, "WorkflowsBoard")));
        Assert.Equal((string)GoldenFiles.Node("workflow-instances", "empty")["empty"]!, Named<TextBlock>(window, "WorkflowsBoardEmpty").Text);
        Frame(window, "workflows-board-empty", Prefix);
    });

    [Fact]
    public Task Empty_lanes_teach_what_belongs_in_them() => OnUi(async () =>
    {
        Lines();
        var (window, _) = await OnBoard(1440, Drawn);

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty).ToList();
        Assert.Contains(texts, t => string.Equals(t, Labels.WorkflowsLaneMergeEmpty, StringComparison.Ordinal));   // Merge has no line state yet: it stays empty and says what waits there
        Assert.Contains(texts, t => string.Equals(t, Labels.WorkflowsLaneEmptyBody, StringComparison.Ordinal));   // Plan is empty in the golden
    });
}
