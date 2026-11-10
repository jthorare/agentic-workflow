using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The From elsewhere line under the Todo panel (todo spec §3.1, §3.4, §3.5, D4): its words, plurals, placeholders, failures and links.</summary>
[Lane(TestLane.Unit)]
public sealed class FromElsewhereTests
{
    private static readonly Uri _url = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A line that has heard from Reviews and Linear (the fake serves 3 assigned issues).
    private async Task<FromElsewhereViewModel> Ready(int reviews = 2, bool reviewsFailed = false)
    {
        var line = new FromElsewhereViewModel(_server);
        line.SetReviews(loading: false, reviews, reviewsFailed);
        await line.Refresh(_url, Token);
        return line;
    }

    private void Assigned(int total)
    {
        var list = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("linear-issues", "filtered-empty"))!;
        list["total"] = total;
        _server.LinearIssuesAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }

    [Fact]
    public async Task The_line_names_the_three_counts_in_order()
    {
        Assigned(1);

        var line = await Ready();

        Assert.Equal("2 PRs waiting on your review · 1 Linear issue assigned", line.Text);
        Assert.Equal([ElsewherePart.Reviews, ElsewherePart.Linear], line.Parts.Select(p => p.Part));
        Assert.False(line.IsLoading);
    }

    [Fact]
    public async Task One_of_each_reads_singular()
    {
        Assigned(1);

        var line = await Ready(reviews: 1);

        Assert.Equal("1 PR waiting on your review · 1 Linear issue assigned", line.Text);
    }

    [Fact]
    public async Task Many_read_plural()
    {
        Assigned(4);

        var line = await Ready(reviews: 5);

        Assert.Equal("5 PRs waiting on your review · 4 Linear issues assigned", line.Text);
    }

    [Theory]
    [InlineData(0, 1, "1 Linear issue assigned")]
    [InlineData(2, 0, "2 PRs waiting on your review")]
    [InlineData(2, 1, "2 PRs waiting on your review · 1 Linear issue assigned")]
    public async Task A_part_with_a_zero_count_is_left_out(int reviews, int linear, string expected)
    {
        Assigned(linear);

        var line = await Ready(reviews);

        Assert.Equal(expected, line.Text);
    }

    [Fact]
    public async Task All_zero_says_nothing_waiting_elsewhere_and_draws_no_part()
    {
        Assigned(0);

        var line = await Ready(reviews: 0);

        Assert.Equal("Nothing waiting elsewhere", line.Text);
        Assert.Empty(line.Parts);
        Assert.False(line.IsLoading);
    }

    [Fact]
    public void Before_the_first_answers_the_line_says_it_is_checking()
    {
        var line = new FromElsewhereViewModel(_server);

        Assert.True(line.IsLoading);
        Assert.Equal("Checking reviews and Linear…", line.Text);
        Assert.Empty(line.Parts);
    }

    [Fact]
    public async Task It_keeps_checking_until_both_reads_have_answered()
    {
        Assigned(1);
        var line = new FromElsewhereViewModel(_server);
        await line.Refresh(_url, Token);
        Assert.True(line.IsLoading);   // Reviews has not answered

        line.SetReviews(loading: false, waiting: 2, failed: false);

        Assert.False(line.IsLoading);
        Assert.Equal("2 PRs waiting on your review · 1 Linear issue assigned", line.Text);
    }

    [Fact]
    public async Task A_failed_reviews_read_says_so_with_retry_and_the_rest_stay()
    {
        Assigned(1);

        var line = await Ready(reviewsFailed: true);

        Assert.Equal("Reviews didn't load · 1 Linear issue assigned", line.Text);
        var reviews = line.Parts[0];
        Assert.Equal((ElsewherePart.Reviews, true, "Retry Reviews"), (reviews.Part, reviews.CanRetry, reviews.RetryName));
        Assert.All(line.Parts.Skip(1), p => Assert.False(p.CanRetry));
    }

    [Fact]
    public async Task A_failed_linear_read_says_so_with_retry_and_the_rest_stay()
    {
        _server.LinearIssuesAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-X-500", "down");

        var line = await Ready();

        Assert.Equal("2 PRs waiting on your review · Linear didn't load", line.Text);
        Assert.Equal((ElsewherePart.Linear, true, "Retry Linear"), (line.Parts[1].Part, line.Parts[1].CanRetry, line.Parts[1].RetryName));
    }

    [Fact]
    public async Task Retry_on_linear_reads_again_and_clears_the_failure()
    {
        _server.LinearIssuesAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-X-500", "down");
        var line = await Ready();
        Assigned(2);

        await line.Retry(ElsewherePart.Linear);

        Assert.Equal("2 PRs waiting on your review · 2 Linear issues assigned", line.Text);
    }

    [Fact]
    public async Task Retry_on_reviews_asks_the_shell_to_read_again()
    {
        Assigned(0);
        var line = await Ready(reviewsFailed: true);
        var asked = 0;
        line.ReviewsRetryRequested += (_, _) => asked++;

        await line.Retry(ElsewherePart.Reviews);

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task A_linear_that_is_not_connected_says_so_and_opens_the_connection_instead()
    {
        _server.LinearIssuesAnswer = RouteAnswer.Api("linear-issues", "not-connected");
        var line = await Ready();
        var (connect, opened) = (0, new List<ElsewherePart>());
        line.ConnectRequested += (_, _) => connect++;
        line.OpenRequested += (_, e) => opened.Add(e.Part);

        line.Open(ElsewherePart.Linear);

        Assert.Equal("2 PRs waiting on your review · Connect Linear", line.Text);
        Assert.Equal((1, 0), (connect, opened.Count));
        Assert.Equal("Opens Connections: Connect Linear", line.Parts[1].Name);
    }

    [Fact]
    public async Task Each_part_opens_its_own_place()
    {
        Assigned(1);
        var line = await Ready();
        var opened = new List<ElsewherePart>();
        line.OpenRequested += (_, e) => opened.Add(e.Part);

        line.Open(ElsewherePart.Reviews);
        line.Open(ElsewherePart.Linear);

        Assert.Equal([ElsewherePart.Reviews, ElsewherePart.Linear], opened);
    }

    [Fact]
    public async Task The_parts_have_link_names_for_a_screen_reader()
    {
        Assigned(1);

        var line = await Ready();

        Assert.Equal(
            [
                "Opens Reviews: 2 PRs waiting on your review",
                "Opens Linear: 1 Linear issue assigned",
            ],
            line.Parts.Select(p => p.Name),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_new_count_replaces_the_old_one()
    {
        Assigned(0);
        var line = await Ready(reviews: 2);

        line.SetReviews(loading: false, waiting: 0, failed: false);

        Assert.Equal("Nothing waiting elsewhere", line.Text);
    }

    [Fact]
    public async Task The_line_never_counts_the_Needs_you_rows_because_that_panel_is_right_below()
    {
        Assigned(1);

        var line = await Ready();

        Assert.DoesNotContain("need you", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void While_the_views_are_stalled_and_nothing_has_answered_the_line_says_what_is_stale_not_Checking()
    {
        var line = new FromElsewhereViewModel(_server);

        line.SetViewsStalled(stalled: true);

        Assert.Equal(("Reviews and Linear wait for Sindri to catch up", false, 0), (line.Text, line.IsLoading, line.Parts.Count));
    }

    [Fact]
    public async Task A_stall_that_clears_goes_back_to_Checking_and_then_to_the_counts()
    {
        Assigned(1);
        var line = new FromElsewhereViewModel(_server);
        line.SetViewsStalled(stalled: true);

        line.SetViewsStalled(stalled: false);
        Assert.Equal(("Checking reviews and Linear…", true), (line.Text, line.IsLoading));
        line.SetReviews(loading: false, waiting: 2, failed: false);
        await line.Refresh(_url, Token);

        Assert.Equal("2 PRs waiting on your review · 1 Linear issue assigned", line.Text);
    }

    [Fact]
    public async Task Feeding_the_same_values_again_raises_nothing()
    {
        Assigned(1);
        var line = await Ready(reviews: 2);
        var parts = line.Parts;
        var raised = new List<string?>();
        line.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        line.SetReviews(loading: false, waiting: 2, failed: false);
        await line.Refresh(_url, Token);

        Assert.Empty(raised);
        Assert.Same(parts, line.Parts);
    }

    [Fact]
    public async Task A_changed_count_still_raises_Parts_and_Text()
    {
        Assigned(1);
        var line = await Ready(reviews: 2);
        var raised = new List<string?>();
        line.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        line.SetReviews(loading: false, waiting: 3, failed: false);

        Assert.Contains(raised, r => string.Equals(r, nameof(FromElsewhereViewModel.Parts), StringComparison.Ordinal));
        Assert.Contains(raised, r => string.Equals(r, nameof(FromElsewhereViewModel.Text), StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_bare_404_from_an_older_sindri_draws_no_linear_part_and_is_not_failed_or_loading()
    {
        _server.LinearIssuesAnswer = new RouteAnswer(HttpStatusCode.NotFound, string.Empty);

        var line = await Ready();

        Assert.Equal("2 PRs waiting on your review", line.Text);
        Assert.DoesNotContain(line.Parts, p => p.Part == ElsewherePart.Linear);
        Assert.False(line.IsLoading);
    }
}
