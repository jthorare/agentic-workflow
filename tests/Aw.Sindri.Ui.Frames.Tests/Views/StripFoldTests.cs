using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The fold (screens §4.1): at 1280 × 800 Home's summary, Needs you, the strip and the column tops are all above the fold.</summary>
[Lane(TestLane.Integration)]
public sealed class StripFoldTests : FrameHost
{
    private static double Top(Window window, string name) => Named<Control>(window, name).TranslatePoint(new Point(0, 0), window)!.Value.Y; // why: the panels are in the tree

    private static int StripRows(Window window) => window.GetVisualDescendants().OfType<Border>().Count(b => b.Name == "StripRowFrame" && b.IsEffectivelyVisible);

    [Fact]
    public Task At_1280x800_summary_needs_you_5_strip_3_and_each_strip_row_is_one_line() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 5 && s.Strip.Rows.Count > 0, width: 1280, height: 800);

        Assert.Equal((5, 3), (shell.NeedsYou.Rows.Count, StripRows(window)));
        var rows = window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "StripRowFrame" && b.IsEffectivelyVisible);
        Assert.All(rows, r => Assert.InRange(r.Bounds.Height, 0, 80)); // why: the right column is half the page (F3 home spec), so the stage and step may wrap under the title
        var needs = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("needs-you-row") && b.IsEffectivelyVisible);
        Assert.All(needs, r => Assert.InRange(r.Bounds.Height, 0, 64)); // why: two lines a row, the fold (provisional owner default)
        Assert.InRange((int)Top(window, "RunningPanel"), 0, 799); // why: the fold holds the columns' tops with 5 + 3 rows (provisional owner default: two-line Needs-you rows)
        Assert.InRange((int)Top(window, "QueuedPanel"), 0, 799);
    });

    [Fact]
    public Task At_1280x800_the_fixture_list_and_five_strip_rows_keep_the_column_tops_above_the_fold() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "fixture");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0 && s.Strip.Rows.Count > 0, width: 1280, height: 800);

        Assert.Equal(5, StripRows(window));
        Assert.InRange((int)Top(window, "RunningPanel"), 0, 799);
        Assert.InRange((int)Top(window, "QueuedPanel"), 0, 799);
        Assert.True(shell.NeedsYou.Count > 0);
    });

    [Fact]
    public Task At_scale_1_3_needs_you_4_and_strip_2_fit() => OnUi(async () =>
    {
        try
        {
            SettingsTheme.For(Application.Current!.Resources)(new AppearanceSettings(1.3, ReduceMotion: false)); // why: the headless session starts the app
            Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
            var (window, shell) = await Connected(s => s.NeedsYou.Count > 5 && s.Strip.Rows.Count > 0, width: 1280, height: 800);
            shell.NeedsYou.TextScale = 1.3;
            shell.Strip.TextScale = 1.3;
            Settle(window);

            Assert.Equal((4, 2), (shell.NeedsYou.Rows.Count, StripRows(window)));
        }
        finally
        {
            SettingsTheme.For(Application.Current!.Resources)(new AppearanceSettings(1.0, ReduceMotion: false));
        }
    });
}
