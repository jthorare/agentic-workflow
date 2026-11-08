using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The diff keys and the fix-task sheet in the window (review page spec §6.2 "Sheets", §6.4; mockups sheet-fix-line-*, sheet-fix-check-1440, sheet-fix-o1-*): focus in, focus out, the width rule.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewFixViewTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnPage(double width)
    {
        var (window, shell) = await Connected(width: width, height: 900);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready && Visible(window, "ReviewDiffPane"), "the review page never showed");
        return (window, shell);
    }

    private static object? Focused(Window window) => TopLevel.GetTopLevel(window)?.FocusManager?.GetFocusedElement();

    private static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    private static Control FirstLine(Window window) =>
        window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "ReviewDiffLine" && c.IsEffectivelyVisible && c.DataContext is ReviewDiffLineViewModel);

    [Fact]
    public Task C_on_a_focused_diff_line_opens_the_sheet_with_its_text_box_focused_and_Cancel_returns_to_the_line() => OnUi(async () =>
    {
        var (window, shell) = await OnPage(1440);
        var line = FirstLine(window);
        _ = line.Focus(NavigationMethod.Tab);

        Press(line, Key.C);

        await Until(window, () => shell.IsFixTaskSheet && shell.IsSheetOpen && Visible(window, "ReviewFixText"), "the fix sheet never opened");
        Assert.Same(Named<TextBox>(window, "ReviewFixText"), Focused(window));
        Named<Button>(window, "ReviewFixCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => !shell.IsSheetOpen && ReferenceEquals(Focused(window), line), "focus never came back to the diff line");
    });

    [Fact]
    public Task Keys_typed_in_the_sheets_text_box_never_move_the_diff() => OnUi(async () =>
    {
        var (window, shell) = await OnPage(1440);
        shell.ReviewPage.RequestCheckFix("ci-ui");
        await Until(window, () => Visible(window, "ReviewFixText"), "the fix sheet never opened");
        var box = Named<TextBox>(window, "ReviewFixText");

        Press(box, Key.J);

        Assert.Null(shell.ReviewPage.Diff.FocusedFile);
    });

    [Fact]
    public Task Send_shows_the_outcome_text_and_focus_goes_to_it() => OnUi(async () =>
    {
        var (window, shell) = await OnPage(1440);
        shell.ReviewPage.RequestCheckFix("ci-ui");
        await Until(window, () => Visible(window, "ReviewFixSend"), "the fix sheet never opened");

        Named<Button>(window, "ReviewFixSend").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await Until(window, () => Visible(window, "ReviewFixOutcome") && ReferenceEquals(Focused(window), Named<TextBlock>(window, "ReviewFixOutcome")), "focus never went to the outcome text");
        Assert.Equal("No session is on feat/retry-budget. The item waits in Work as \"Waiting on feat/retry-budget\", and the first session on that branch is told.", Named<TextBlock>(window, "ReviewFixOutcome").Text);
    });

    [Fact]
    public Task At_1000_px_the_fix_sheet_covers_the_page() => OnUi(async () =>
    {
        var (window, shell) = await OnPage(1000);

        shell.ReviewPage.RequestCheckFix("ci-ui");

        await Until(window, () => shell.IsFixTaskSheet && Visible(window, "ReviewFixText"), "the fix sheet never opened");
        var sheet = window.Get<Border>("Sheet");
        Assert.Contains("covers", sheet.Classes, StringComparer.Ordinal);
        Assert.False(window.Get<Border>("Page").IsEffectivelyEnabled);
        Assert.True(sheet.IsVisualAncestorOf(Named<TextBox>(window, "ReviewFixText")));
    });
}
