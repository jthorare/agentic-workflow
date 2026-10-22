using Aw.Kernel.Testing;
using System.Net;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The Recipes page's served run figures (API P12, E-25): a row's runs, finished share and typical time, a dash where none are served, and the 7, 30 or 90 day window.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipesPageStatsTests
{
    private const string Hash = "sha256:e204c3b4ff1755b975bc92713ab9a7f4ca9ea54a8888a5b32b06bba4fac8c636";

    private readonly RecipesPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RecipeSummaryView Served(RecipeListStats? stats, bool childOnly = false) =>
        new("workflow.phase", Version: 1, Hash, "approved", Name: null, DisplayName: null, Stale: [], Inputs: [], Outputs: [], States: [], Status: "approved", ChildOnly: childOnly, Stats: stats);

    private static RecipeListStats Figures(int runs, int done, long? median) => new(runs, done, Failed: 0, HumanRate: null, median, TokensP50: null, Buckets: []);

    [Fact]
    public void A_row_reads_runs_the_finished_share_and_the_typical_time()
    {
        var row = new RecipeRowViewModel(Served(Figures(runs: 10, done: 8, median: 2700)));

        Assert.Equal(("10", "80%", "45m"), (row.RunsText, row.FinishedText, row.TypicalText));
    }

    [Fact]
    public void A_row_with_no_stats_shows_dashes()
    {
        var row = new RecipeRowViewModel(Served(stats: null));

        Assert.Equal(("–", "–", "–"), (row.RunsText, row.FinishedText, row.TypicalText));
    }

    [Fact]
    public void A_row_with_no_runs_says_zero_and_has_no_share_or_time()
    {
        var row = new RecipeRowViewModel(Served(Figures(runs: 0, done: 0, median: null)));

        Assert.Equal(("0", "–", "–"), (row.RunsText, row.FinishedText, row.TypicalText));
    }

    [Fact]
    public void A_workflow_that_only_runs_inside_others_says_so()
    {
        var row = new RecipeRowViewModel(Served(stats: null, childOnly: true));

        Assert.Equal(("–", "Runs inside others", "–"), (row.RunsText, row.FinishedText, row.TypicalText));
    }

    [Fact]
    public void New_figures_replace_the_row_a_re_read_kept()
    {
        var before = new RecipeRowViewModel(Served(Figures(runs: 1, done: 1, median: 60)));
        var after = new RecipeRowViewModel(Served(Figures(runs: 2, done: 1, median: 60)));

        Assert.False(string.Equals(before.Signature, after.Signature, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_page_reads_the_served_list_with_its_stats_at_thirty_days_to_start()
    {
        var page = await _f.Shown(Token);

        Assert.Equal(("30d", "30d"), (page.Window, _f.Server.LastListWindow));
        Assert.Equal(("0", "–", "–"), (page.Rows[0].RunsText, page.Rows[0].FinishedText, page.Rows[0].TypicalText));   // why: the list golden's first recipe has no runs
        Assert.Equal(3, page.Windows.Count);
        Assert.Equal("30d", Assert.Single(page.Windows, w => w.IsChosen).Window);
    }

    [Fact]
    public async Task Choosing_a_window_reads_the_list_again_at_it()
    {
        var page = await _f.Shown(Token);
        var before = _f.Server.RecipesCalls;

        await page.ChooseWindow("7d", Token);

        Assert.Equal(("7d", "7d", before + 1), (page.Window, _f.Server.LastListWindow, _f.Server.RecipesCalls));
        Assert.Equal("7d", Assert.Single(page.Windows, w => w.IsChosen).Window);
    }

    [Theory]
    [InlineData("1y")]
    [InlineData("30d")]
    public async Task A_window_the_api_does_not_serve_or_the_one_shown_reads_nothing(string window)
    {
        var page = await _f.Shown(Token);
        var before = _f.Server.RecipesCalls;

        await page.ChooseWindow(window, Token);

        Assert.Equal(("30d", before), (page.Window, _f.Server.RecipesCalls));
    }

    [Fact]
    public async Task A_window_chosen_while_the_page_is_not_shown_waits_for_it()
    {
        var page = _f.Page();
        await page.Refresh(RecipesPageFixture.Url, Token);

        await page.ChooseWindow("90d", Token);
        page.SetShown(shown: true);
        await page.CatchUp;

        Assert.Equal(("90d", "90d"), (page.Window, _f.Server.LastListWindow));
    }

    [Fact]
    public async Task A_list_that_serves_no_stats_still_shows_its_rows()
    {
        _f.Server.RecipeListAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"recipes\":[" + RecipesPageFixture.Row("workflow.phase") + "]}");

        var page = await _f.Shown(Token);

        Assert.Equal(("–", false), (page.Rows[0].RunsText, page.IsUnreadable));
    }
}
