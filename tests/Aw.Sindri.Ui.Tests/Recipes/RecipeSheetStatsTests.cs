using Aw.Kernel.Testing;
using System.Net;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The recipe sheet's run figures (API P12, E-24): the shown version's runs, finished share, needed-a-person share and typical time over a window, or a line saying why there are none.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeSheetStatsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private const string Recipe = "workflow.dev-triage-three-state";
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Hash12 => ((string)GoldenFiles.Node("workflow-recipe", Recipe)["hash"]!)["sha256:".Length..][..12];

    private async Task<RecipeSheetViewModel> Opened()
    {
        var sheet = new RecipeSheetViewModel(_server);
        await sheet.Open(_api, Recipe, Token);
        return sheet;
    }

    [Fact]
    public async Task The_sheet_reads_the_stats_of_the_shown_version_at_thirty_days()
    {
        _ = await Opened();

        Assert.Equal((Recipe, "30d", Hash12), _server.LastStatsRead);
    }

    [Fact]
    public async Task The_figures_read_in_words()
    {
        var sheet = await Opened();

        Assert.Equal(
            [("Runs", "4"), ("Finished", "50%"), ("Needed a person", "25%"), ("Typical time", "45m"), ("Failed", "1"), ("Cancelled", "1"), ("Typical tokens", "1,000")],
            sheet.StatLines.Select(l => (l.Label, l.Value)));
        Assert.False(sheet.HasStatsNote);
    }

    [Fact]
    public async Task Choosing_a_window_reads_the_figures_again_at_it()
    {
        var sheet = await Opened();

        await sheet.ChooseStatsWindow("90d", Token);

        Assert.Equal((Recipe, "90d", Hash12), _server.LastStatsRead);
        Assert.Equal(("90d", "90d"), (sheet.StatsWindow, Assert.Single(sheet.StatsWindows, w => w.IsChosen).Window));
    }

    [Fact]
    public async Task A_window_the_api_does_not_serve_changes_nothing()
    {
        var sheet = await Opened();

        await sheet.ChooseStatsWindow("1y", Token);

        Assert.Equal(("30d", "30d"), (sheet.StatsWindow, _server.LastStatsRead?.Window));
    }

    [Fact]
    public async Task A_window_with_no_runs_says_so_instead_of_listing_zeros()
    {
        _server.RecipeStatsAnswer = Served("""{"recipe":"workflow.x","hash":null,"window":"30d","childOnly":false,"runs":0,"done":0,"failed":0,"cancelled":0,"outcomes":null,"humanRate":null,"medianSeconds":null,"tokens":null,"budgetHits":null,"buckets":[],"asOf":null}""");

        var sheet = await Opened();

        Assert.Equal(("No runs in this window.", 0), (sheet.StatsNote, sheet.StatLines.Count));
    }

    [Fact]
    public async Task A_workflow_that_only_runs_inside_others_says_so()
    {
        _server.RecipeStatsAnswer = Served("""{"recipe":"workflow.x","hash":null,"window":"30d","childOnly":true,"runs":null,"done":null,"failed":null,"cancelled":null,"outcomes":null,"humanRate":null,"medianSeconds":null,"tokens":null,"budgetHits":null,"buckets":[],"asOf":null}""");

        var sheet = await Opened();

        Assert.Equal(("This workflow only runs inside other workflows, so it has no runs of its own.", 0), (sheet.StatsNote, sheet.StatLines.Count));
    }

    [Fact]
    public async Task Figures_that_cannot_be_read_leave_the_sheet_shown_with_a_line()
    {
        _server.RecipeStatsAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"runs\":\"nope\"}");

        var sheet = await Opened();

        Assert.Equal((true, "Couldn't read how this workflow has run."), (sheet.IsShown, sheet.StatsNote));
    }

    private static RouteAnswer Served(string body) => new(HttpStatusCode.OK, body);
}
