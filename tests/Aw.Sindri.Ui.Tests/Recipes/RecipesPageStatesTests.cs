using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>Every state of the Recipes page (recipes spec §3): reading, no workflows, nothing matches, out of date, can't read the answer, can't reach Sindri.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipesPageStatesTests
{
    private readonly RecipesPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Before_the_first_answer_the_page_reads()
    {
        var page = _f.Page();

        Assert.Equal((true, "Reading workflows…"), (page.IsReading, page.ReadingText));
        Assert.False(page.IsEmpty);
    }

    [Fact]
    public async Task An_empty_list_says_no_workflows_are_installed()
    {
        _f.Serve();

        var page = await _f.Shown(Token);

        Assert.Equal((false, true, "No workflows are installed"), (page.IsReading, page.IsEmpty, page.EmptyText));
        Assert.False(page.IsNoMatch);
    }

    [Fact]
    public async Task A_read_that_fails_after_a_good_one_keeps_the_rows_and_marks_them_out_of_date()
    {
        _f.ServeThree();
        var page = await _f.Shown(Token);

        _f.Server.RecipeListAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");
        await page.Refresh(RecipesPageFixture.Url, Token);

        Assert.Equal((true, false, 3), (page.IsStale, page.IsFailed, page.Rows.Count));
    }

    [Fact]
    public async Task A_first_read_that_fails_says_so_and_offers_to_try_again()
    {
        _f.Server.RecipeListAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");
        var page = await _f.Shown(Token);

        Assert.Equal((true, false), (page.IsFailed, page.IsReading));
        Assert.StartsWith("Couldn't read the workflows.", page.FailedText, StringComparison.Ordinal);

        _f.ServeThree();
        await page.Retry(Token);

        Assert.Equal((false, 3), (page.IsFailed, page.Rows.Count));
    }

    [Fact]
    public async Task An_answer_this_build_cannot_read_gets_its_own_box()
    {
        _f.Server.RecipeListAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"recipes\":\"nope\"}");

        var page = await _f.Shown(Token);

        Assert.Equal((true, false, "This app can't read this workflow's answer."), (page.IsUnreadable, page.IsFailed, page.UnreadableText));
    }

    [Fact]
    public async Task The_footer_says_the_usage_figures_are_not_served()
    {
        _f.ServeThree();

        var page = await _f.Shown(Token);

        Assert.Equal("Only approved workflows can start a run. Runs, success rate and average time per workflow are not served yet.", page.FooterText);
    }

    [Fact]
    public void The_page_and_the_nav_entry_are_each_named_by_one_constant()
    {
        var page = _f.Page();

        Assert.Equal(Aw.Sindri.Ui.ViewModels.Labels.RecipesPageTitle, page.Title);
        Assert.Equal(Aw.Sindri.Ui.ViewModels.Labels.Recipes, Aw.Sindri.Ui.ViewModels.Labels.Page(Aw.Sindri.Ui.Navigation.PageId.Recipes));
    }
}
