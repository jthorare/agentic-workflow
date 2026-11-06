using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>The Reviews page in the window (mockups list-main, list-state-nologin, list-state-empty): the two group headings, the login prompt and the empty lines.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewsPageViewTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnReviews(string? scenario = null)
    {
        if (scenario is not null)
        {
            Server.ReviewsAnswer = RouteAnswer.Api("reviews", scenario);
        }

        var (window, shell) = await Connected();
        shell.GoTo(PageId.Reviews);
        await Until(window, () => !shell.Reviews.IsLoading, "the Reviews page never read the list");
        return (window, shell);
    }

    [Fact]
    public Task The_list_draws_a_heading_per_group_and_a_frame_per_row() => OnUi(async () =>
    {
        var (window, _) = await OnReviews();

        var headings = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "ReviewsGroupHeading" && t.IsEffectivelyVisible).Select(t => t.Text);
        Assert.Equal(["Waiting on you", "Yours"], headings, StringComparer.Ordinal);
        Assert.Equal(4, window.GetVisualDescendants().OfType<Border>().Count(b => b.Name == "ReviewRowFrame" && b.IsEffectivelyVisible));
    });

    [Fact]
    public Task No_login_draws_the_prompt_and_no_groups() => OnUi(async () =>
    {
        var (window, _) = await OnReviews("no-login");

        Assert.True(Visible(window, "ReviewsLogin"));
        Assert.Equal("joi-example", Named<TextBox>(window, "ReviewsLoginField").Text);
        Assert.False(Named<Button>(window, "ReviewsLoginSave").IsEnabled);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "ReviewsGroupHeading");
    });

    [Fact]
    public Task No_rows_draws_the_empty_lines() => OnUi(async () =>
    {
        var (window, _) = await OnReviews("empty");

        Assert.True(Visible(window, "ReviewsEmpty"));
        Assert.False(Visible(window, "ReviewsLogin"));
    });

    [Fact]
    public Task The_header_button_opens_the_library_sheet_with_its_sections() => OnUi(async () =>
    {
        Server.LibraryAnswer = RouteAnswer.Api("library", "with-errors");
        var (window, shell) = await OnReviews();
        Assert.False(Visible(window, "ReviewsLibrarySummary"));

        Named<Button>(window, "ReviewsLibrary").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.IsLibrarySheet && shell.IsSheetOpen && !shell.Reviews.Library.IsLoading && Visible(window, "ReviewsLibrarySummary"), "the library sheet never showed");

        var titles = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "ReviewsLibrarySectionTitle" && t.IsEffectivelyVisible).Select(t => t.Text);
        Assert.Equal(["Docs with errors"], titles, StringComparer.Ordinal);
        Assert.Equal("41 docs, 3 with errors", Named<TextBlock>(window, "ReviewsLibrarySummary").Text);

        shell.CloseSheet();
        await Until(window, () => !Visible(window, "ReviewsLibrarySummary") && !shell.Reviews.Library.IsOpen, "the library sheet never closed");
    });

    [Fact]
    public Task J_and_K_move_the_row_focus_and_a_refresh_leaves_it_on_the_same_PR() => OnUi(async () =>
    {
        var (window, shell) = await OnReviews();
        List<Border> Rows() => [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "ReviewRowFrame" && b.IsEffectivelyVisible)];
        _ = Rows()[0].Focus(NavigationMethod.Tab);
        Rows()[0].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.J, Source = Rows()[0] });
        var focused = Rows().Single(r => r.IsKeyboardFocusWithin);
        var pr = ((ViewModels.Reviews.ReviewRowViewModel)focused.DataContext!).Ref;
        Assert.Equal(Rows()[1], focused);

        await shell.Reviews.Refresh(new Uri("http://127.0.0.1:7410/"), TestContext.Current.CancellationToken);
        await Task.Yield();

        var after = Rows().Single(r => r.IsKeyboardFocusWithin);
        Assert.Equal(pr, ((ViewModels.Reviews.ReviewRowViewModel)after.DataContext!).Ref);
        Assert.Same(focused, after);
    });

    [Fact]
    public Task The_column_headings_read_the_labels() => OnUi(async () =>
    {
        var (window, _) = await OnReviews();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        string[] heads = [ViewModels.Labels.ReviewsColPr, ViewModels.Labels.ReviewsColAuthor, ViewModels.Labels.ReviewsColAge, ViewModels.Labels.ReviewsColCi, ViewModels.Labels.ReviewsColRequired, ViewModels.Labels.ReviewsColDoc];
        Assert.Equal(heads, texts.Where(t => heads.Contains(t, StringComparer.Ordinal)).Take(6), StringComparer.Ordinal);
    });
}
