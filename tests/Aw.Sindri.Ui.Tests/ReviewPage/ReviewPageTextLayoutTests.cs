using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The text layout check of #2228 over the diff pane of the review page with the fix sheet open: the gap check on the pane header (diff line gutters touch by design) and the right-edge check on every text and button of the pane, and the check's own proof that it rejects the bad one-row grid.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewPageTextLayoutTests : FrameHost
{
    [Theory]
    [InlineData(1280, false)]
    [InlineData(1440, false)]
    [InlineData(1280, true)]
    [InlineData(1440, true)]
    public Task With_the_fix_sheet_open_the_diff_pane_has_no_cut_off_or_touching_texts(double width, bool wholePane) => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: width, height: 900);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready && Visible(window, "ReviewDiffPane"), "the review page never showed");
        var line = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "ReviewDiffLine" && c.IsEffectivelyVisible && c.DataContext is ReviewDiffLineViewModel);
        _ = line.Focus(NavigationMethod.Tab);
        line.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, Source = line });
        await Until(window, () => shell.IsFixTaskSheet && shell.IsSheetOpen && Visible(window, "ReviewFixText"), "the fix sheet never opened");
        await Until(window, () => Named<Control>(window, "ReviewDiffPane").Bounds.Width > 0, "the diff pane never got a width");

        // The gap check stays on the header: diff line gutters touch by design. The right-edge check covers every text and button of the pane.
        var problems = wholePane
            ? TextRowCheck.CutOffControls(Named<Control>(window, "ReviewDiffPane"))
            : TextRowCheck.Problems(Named<Control>(window, "ReviewDiffHeader"));
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    });

    [Fact]
    public Task The_check_rejects_two_long_texts_in_an_Auto_star_Auto_grid_in_200_px() => OnUi(async () =>
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(new TextBlock { Text = "Files changed and more" });
        var hint = new TextBlock { Text = "j/k file · n/p hunk · c fix task · o follow" };
        Grid.SetColumn(hint, 2);
        grid.Children.Add(hint);
        var host = new StackPanel { Width = 200, Children = { grid } };
        var window = new Window { Width = 400, Height = 200, Content = host };
        window.Show();
        try
        {
            await Task.Yield();
            window.UpdateLayout();
            Assert.NotEmpty(TextRowCheck.Problems(host));
        }
        finally
        {
            window.Close();
        }
    });
}
