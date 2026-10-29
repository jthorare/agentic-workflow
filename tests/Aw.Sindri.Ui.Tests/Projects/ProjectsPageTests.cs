using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page (projects page spec §3, §3.1): the groups, the folds, the freshness line, the served sentence and when the page reads.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsPageTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_page_is_loading_until_its_first_answer()
    {
        var page = _f.Page();
        page.SetShown(shown: true);
        Assert.True(page.IsLoading);

        await page.Refresh(Url, Token);

        Assert.False(page.IsLoading);
    }

    [Fact]
    public async Task The_mixed_list_has_a_group_per_state_in_order_with_its_cards()
    {
        var page = await _f.Shown(Token);

        Assert.Equal(["In progress", "Planned", "Paused"], page.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(["Checkout rewrite", "Kitchen", "Mobile app"], page.Groups.Select(g => Assert.Single(g.Cards).Name), StringComparer.Ordinal);
        Assert.All(page.Groups, g => Assert.Equal(1, g.Count));
        Assert.Equal("by hand", page.Groups[1].Cards[0].OriginChip);
        var mobile = page.Groups[2].Cards[0];
        Assert.Equal(("attached", "Waiting on the vendor"), (mobile.OriginChip, mobile.NotesFace));
        Assert.Null(page.Groups[0].Cards[0].OriginChip);
    }

    [Fact]
    public async Task A_state_the_page_does_not_know_gets_a_last_group_headed_by_its_word()
    {
        var list = Golden("mixed");
        list["projects"]![0]!["state"] = "someday";
        _f.Serve(list);

        var page = await _f.Shown(Token);

        Assert.Equal(["Planned", "Paused", "Someday"], page.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal("Checkout rewrite", Assert.Single(page.Groups[2].Cards).Name);
    }

    [Fact]
    public async Task The_cards_stay_in_the_order_the_server_serves()
    {
        var list = Golden("mixed");
        var second = list["projects"]![0]!.DeepClone();
        second["id"] = "linear:project/00000000-0000-4000-8000-0000000000aa";
        second["name"] = "Zebra";
        list["projects"]!.AsArray().Insert(1, second);
        _f.Serve(list);

        var page = await _f.Shown(Token);

        Assert.Equal(["Checkout rewrite", "Zebra"], page.Groups[0].Cards.Select(c => c.Name), StringComparer.Ordinal);
        Assert.Equal(2, page.Groups[0].Count);
    }

    [Fact]
    public async Task The_folds_count_their_cards_and_start_closed()
    {
        var page = await _f.Shown(Token);

        Assert.Equal(("Finished", 1, "Done or canceled in the last 14 days", false, true), (page.Finished.Title, page.Finished.Count, page.Finished.Why, page.Finished.IsOpen, page.Finished.IsVisible));
        Assert.Equal(("Hidden", 1, string.Empty, false, true), (page.Hidden.Title, page.Hidden.Count, page.Hidden.Why, page.Hidden.IsOpen, page.Hidden.IsVisible));
        Assert.Equal("Done Dec 29", page.Finished.Cards[0].Footer[0]);
        Assert.Equal("Billing cleanup", page.Hidden.Cards[0].Name);
    }

    [Fact]
    public async Task A_fold_with_no_cards_is_not_visible()
    {
        _f.Serve("syncing");

        var page = await _f.Shown(Token);

        Assert.Equal((0, false, 0, false), (page.Finished.Count, page.Finished.IsVisible, page.Hidden.Count, page.Hidden.IsVisible));
    }

    [Fact]
    public async Task A_fold_stays_open_across_a_refresh()
    {
        var page = await _f.Shown(Token);
        page.Finished.Toggle();
        Assert.True(page.Finished.IsOpen);

        await page.Refresh(Url, Token);

        Assert.True(page.Finished.IsOpen);
        Assert.Equal(1, page.Finished.Count);
        page.Finished.Toggle();
        Assert.False(page.Finished.IsOpen);
    }

    [Fact]
    public async Task The_freshness_line_joins_the_sources_and_the_viewer()
    {
        var page = await _f.Shown(Token);

        Assert.Equal("Linear projects synced 14 min ago · Linear issues synced 14 min ago · GitHub synced 14 min ago · Linear says you're Jo Smith", page.Freshness);
        Assert.False(page.FreshnessFailing);
    }

    [Fact]
    public async Task A_failing_source_tints_the_freshness_line()
    {
        _f.Serve("linear-failing");

        var page = await _f.Shown(Token);

        Assert.True(page.FreshnessFailing);
        Assert.Contains("Linear issues needs attention (SND-INDEX-401)", page.Freshness, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_source_this_build_does_not_know_keeps_its_served_name()
    {
        var list = Golden("syncing");
        list["freshness"]![0]!["source"] = "calendar";
        _f.Serve(list);

        var page = await _f.Shown(Token);

        Assert.Equal("calendar still syncing · Linear says you're Jo Smith", page.Freshness);
    }

    [Fact]
    public async Task Context_down_shows_names_only_with_the_served_sentence_as_a_banner()
    {
        _f.Serve("context-down");

        var page = await _f.Shown(Token);

        Assert.Equal(("Linear and GitHub data unavailable: Context is not answering", true, true, false), (page.Sentence, page.NamesOnly, page.IsBanner, page.IsEmptyBlock));
        Assert.Equal("Context Context is not answering", page.Freshness);
        Assert.All(page.Groups.SelectMany(g => g.Cards), c => Assert.True(c.NamesOnly));
    }

    [Fact]
    public async Task Linear_still_syncing_is_the_empty_block_without_Add_project()
    {
        _f.Serve("syncing");

        var page = await _f.Shown(Token);

        Assert.Equal(("Linear is still syncing. Projects you lead show up when it's done.", true, false, false, false), (page.Sentence, page.IsEmptyBlock, page.IsBanner, page.ShowsAddInEmpty, page.NamesOnly));
        Assert.Empty(page.Groups);
    }

    [Fact]
    public async Task Leading_nothing_is_the_empty_block_with_Add_project()
    {
        _f.Serve("empty-leads-nothing");

        var page = await _f.Shown(Token);

        Assert.Equal((true, true, false), (page.IsEmptyBlock, page.ShowsAddInEmpty, page.IsBanner));
        Assert.StartsWith("You don't lead any Linear projects", page.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Linear_not_connected_is_the_empty_block_with_Add_project()
    {
        _f.Serve("empty-no-linear");

        var page = await _f.Shown(Token);

        Assert.Equal((true, true), (page.IsEmptyBlock, page.ShowsAddInEmpty));
    }

    [Fact]
    public async Task A_list_with_no_sentence_is_neither_the_block_nor_the_banner()
    {
        var page = await _f.Shown(Token);

        Assert.Equal((null, false, false, false), (page.Sentence, page.IsEmptyBlock, page.IsBanner, page.NamesOnly));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(559, 1)]
    [InlineData(560, 2)]
    [InlineData(1039, 2)]
    [InlineData(1040, 3)]
    [InlineData(2000, 3)]
    public void The_columns_follow_the_page_width(double width, int columns)
    {
        var page = _f.Page();

        page.SetPageWidth(width);

        Assert.Equal(columns, page.Columns);
    }

    [Fact]
    public async Task A_page_that_is_not_shown_does_not_read()
    {
        var page = _f.Page();

        await page.Refresh(Url, Token);

        Assert.Equal(0, _f.Server.ProjectsCalls);
        Assert.True(page.IsLoading);
    }

    [Fact]
    public async Task Becoming_the_page_shown_reads_at_once_without_the_hidden_projects()
    {
        var page = _f.Page();
        await page.Refresh(Url, Token);

        page.SetShown(shown: true);
        await page.CatchUp;

        Assert.Equal((1, false), (_f.Server.ProjectsCalls, _f.Server.LastProjectsAll));
        Assert.False(page.IsLoading);
    }

    [Fact]
    public async Task A_failed_read_keeps_the_last_answer_and_says_so()
    {
        var page = await _f.Shown(Token);
        _f.Server.ProjectsAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");

        await page.Refresh(Url, Token);

        Assert.NotEmpty(page.Error);
        Assert.Equal(3, page.Groups.Count);
        Assert.False(page.IsLoading);
        _f.Serve("mixed");
        await page.Refresh(Url, Token);
        Assert.Empty(page.Error);
    }
}
