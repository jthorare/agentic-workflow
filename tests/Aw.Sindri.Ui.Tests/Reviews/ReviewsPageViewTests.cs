using Avalonia.Controls;
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
}
