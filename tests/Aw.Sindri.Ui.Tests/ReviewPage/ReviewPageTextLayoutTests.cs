using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The text layout check of #2228 over the diff pane header of the review page with the fix sheet open (scoped to the header: the rest of the page has other cut-off texts, the long diff lines and the Show button), and the check's own proof that it rejects the bad one-row grid.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewPageTextLayoutTests : FrameHost
{
    [Theory]
    [InlineData(1280)]
    [InlineData(1440)]
    public Task With_the_fix_sheet_open_no_two_diff_header_texts_touch_and_none_is_cut_off(double width) => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: width, height: 900);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready && Visible(window, "ReviewDiffPane"), "the review page never showed");
        var line = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "ReviewDiffLine" && c.IsEffectivelyVisible && c.DataContext is ReviewDiffLineViewModel);
        _ = line.Focus(NavigationMethod.Tab);
        line.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, Source = line });
        await Until(window, () => shell.IsFixTaskSheet && shell.IsSheetOpen && Visible(window, "ReviewFixText"), "the fix sheet never opened");
        await Until(window, () => Named<Control>(window, "ReviewDiffPane").Bounds.Width > 0, "the diff pane never got a width");

        var problems = TextRowCheck.Problems(Named<Control>(window, "ReviewDiffHeader"));
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
