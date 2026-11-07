using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The Work page's Linear view in each state the spec's table names (Linear hub spec §3, States).</summary>
[Lane(TestLane.Unit)]
public sealed class LinearWorkStatesTests
{
    private readonly LinearFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_first_load_shows_skeleton_rows_until_the_first_answer()
    {
        var work = _f.Work();
        work.Choose(linear: true);
        work.SetShown(shown: true);
        Assert.True(work.IsLoading);

        await work.Refresh(LinearFixture.Url, Token);

        Assert.False(work.IsLoading);
    }

    [Fact]
    public async Task Without_a_Linear_source_the_card_offers_Connect_Linear_and_its_button_asks_for_Settings()
    {
        _f.Mine = RouteAnswer.Api("linear-issues", "not-connected");
        _f.Teams = RouteAnswer.Api("linear-issues", "not-connected");
        var work = await _f.Shown(Token);
        var asked = 0;
        work.ConnectRequested += (_, _) => asked++;

        work.RequestConnect();

        Assert.Equal((true, false, false, 1), (work.ShowsConnect, work.ShowsNoMatch, work.ShowsFinding, asked));
    }

    [Fact]
    public async Task Connected_with_no_viewer_yet_says_Finding_your_issues_not_that_nothing_is_assigned()
    {
        var answer = LinearFixture.Golden("filtered-empty");
        answer["viewer"] = null;
        _f.Mine = LinearFixture.Served(answer);
        _f.Teams = LinearFixture.Nothing("teams");

        var work = await _f.Shown(Token);

        Assert.Equal((true, false, "Finding your issues…"), (work.ShowsFinding, work.ShowsConnect, work.Mine.Empty));
    }

    [Fact]
    public async Task Nothing_assigned_with_the_filters_off_says_so()
    {
        _f.Mine = LinearFixture.Nothing("mine");
        _f.Teams = LinearFixture.Nothing("teams");

        var work = await _f.Shown(Token);

        Assert.Equal(("No open issues assigned to you", false, false), (work.Mine.Empty, work.ShowsNoMatch, work.ShowsFinding));
    }

    [Fact]
    public async Task With_filters_on_and_nothing_matching_the_box_says_so_and_Clear_filters_brings_the_rows_back()
    {
        var work = await _f.Shown(Token);
        _f.Mine = LinearFixture.Nothing("mine");
        _f.Teams = LinearFixture.Nothing("teams");

        work.Filter("team").Pick("ENG");
        await work.Reading;

        Assert.Equal((true, string.Empty, 0), (work.ShowsNoMatch, work.Mine.Empty, work.Mine.Rows.Count));
        Assert.Equal(("No issues match these filters", "Clear filters"), (ViewModels.Labels.LinearNoMatch, ViewModels.Labels.LinearClearFilters));

        _f.Mine = RouteAnswer.Api("linear-issues", "mine");
        work.ClearFilters();
        await work.Reading;

        Assert.Equal((false, 3), (work.ShowsNoMatch, work.Mine.Rows.Count));
    }

    [Fact]
    public async Task A_failing_feed_shows_a_banner_with_the_last_sync_and_the_code_and_keeps_the_rows()
    {
        _f.Mine = RouteAnswer.Api("linear-issues", "feed-failing");

        var work = await _f.Shown(Token);

        Assert.Equal(("Linear sync failing since 22:05 (SND-LINEAR-010)", false), (work.FeedText, work.IsSindriDown));
        Assert.Equal(3, work.Mine.Rows.Count);
    }

    [Fact]
    public async Task A_healthy_feed_has_no_banner_and_no_updating_line()
    {
        var work = await _f.Shown(Token);

        Assert.Equal((string.Empty, string.Empty), (work.FeedText, work.UpdatingText));
    }

    [Fact]
    public async Task A_feed_that_re_reads_says_how_far_it_has_got()
    {
        _f.Mine = RouteAnswer.Api("linear-issues", "rereading");

        var work = await _f.Shown(Token);

        Assert.Equal("Updating issues… 412 of 1,000", work.UpdatingText);
    }

    [Fact]
    public async Task When_Sindri_does_not_answer_the_rows_stay_as_of_the_last_good_read()
    {
        var work = await _f.Shown(Token);
        _f.Clock.Advance(TimeSpan.FromMinutes(7));
        _f.Server.GoDown();

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal((true, "as of 00:00", 3), (work.IsSindriDown, work.AsOfText, work.Mine.Rows.Count));
        Assert.Equal("Sindri isn't answering", ViewModels.Labels.LinearSindriDown);
        Assert.Equal(string.Empty, work.FeedText);
    }

    [Fact]
    public async Task A_first_read_that_fails_ends_the_skeleton_and_says_Sindri_is_not_answering_without_a_time()
    {
        _f.Server.GoDown();

        var work = await _f.Shown(Token);

        Assert.Equal((false, true, string.Empty), (work.IsLoading, work.IsSindriDown, work.AsOfText));
    }

    [Fact]
    public async Task A_read_that_works_again_clears_the_not_answering_line()
    {
        var work = await _f.Shown(Token);
        _f.Server.GoDown();
        await work.Refresh(LinearFixture.Url, Token);
        Assert.True(work.IsSindriDown);
        _f.Server.ComeUp();

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal((false, string.Empty), (work.IsSindriDown, work.AsOfText));
    }

    [Fact]
    public async Task A_refresh_while_Work_is_not_shown_reads_nothing()
    {
        var work = _f.Work();
        work.Choose(linear: true);

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Empty(_f.Server.LinearQueries);
        Assert.True(work.IsLoading);
    }

    [Fact]
    public async Task The_Sindri_view_reads_no_issues()
    {
        var work = _f.Work();
        work.SetShown(shown: true);

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Empty(_f.Server.LinearQueries);
    }
}
