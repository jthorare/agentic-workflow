using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's Linear reads (Linear hub spec §3): the routes, the query escaping and what the goldens carry.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientLinearTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    private static Canned Ok(string slug, string scenario) => new(HttpStatusCode.OK, GoldenFiles.Api(slug, scenario));

    [Fact]
    public async Task LinearIssues_sends_the_group_alone_when_no_filter_is_set()
    {
        var canned = Ok("linear-issues", "mine");

        _ = await Client(canned).LinearIssues(_api, new LinearIssueQuery("mine"), Token);

        Assert.Equal(("/sindri/linear/issues", "?group=mine"), (canned.Seen[0].AbsolutePath, canned.Seen[0].Query));
    }

    [Fact]
    public async Task LinearIssues_sends_every_set_filter_escaped_and_leaves_the_null_ones_out()
    {
        var canned = Ok("linear-issues", "teams-page2");
        var query = new LinearIssueQuery("teams", Team: "ENG", Project: "a b", Cycle: "c&1", State: "open", Assignee: "u/1", Priority: 2, Label: "bug?", After: "cur=1", Limit: 25);

        _ = await Client(canned).LinearIssues(_api, query, Token);

        Assert.Equal("?group=teams&team=ENG&project=a%20b&cycle=c%261&state=open&assignee=u%2F1&priority=2&label=bug%3F&after=cur%3D1&limit=25", canned.Seen[0].Query);
        var some = Ok("linear-issues", "mine");
        _ = await Client(some).LinearIssues(_api, new LinearIssueQuery("mine", State: "open", Limit: 50), Token);
        Assert.Equal("?group=mine&state=open&limit=50", some.Seen[0].Query);
    }

    [Fact]
    public async Task LinearIssue_LinearLookups_and_LinearSince_call_their_routes()
    {
        var canned = Ok("linear-issue", "full");
        var client = Client(canned);

        _ = await client.LinearIssue(_api, "ENG-12", Token);
        _ = await client.LinearIssue(_api, "a/b", Token);
        _ = await client.LinearLookups(_api, "ENG", Token);
        _ = await client.LinearSince(_api, after: null, Token);
        _ = await client.LinearSince(_api, "lin_3f&x", Token);

        Assert.Equal(["/sindri/linear/issues/ENG-12", "/sindri/linear/issues/a%2Fb", "/sindri/linear/lookups/ENG", "/sindri/linear/since", "/sindri/linear/since"], canned.Seen.Select(u => u.AbsolutePath), StringComparer.Ordinal);
        Assert.Equal(["", "", "", "", "?after=lin_3f%26x"], canned.Seen.Select(u => u.Query), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_mine_golden_reads_three_rows_with_the_first_running_and_waiting_on_review()
    {
        var list = (await Client(Ok("linear-issues", "mine")).LinearIssues(_api, new LinearIssueQuery("mine"), Token)).Value;

        Assert.Equal((true, "Joi Example", "ok", "mine", 3, 3, null), (list.Connected, list.Viewer!.Name, list.Feed.State, list.Group, list.Rows.Count, list.Total, list.Next));
        var first = list.Rows[0];
        Assert.Equal(("ENG-12", "waiting", "running", 1, "started", 2, 14), (first.Key, first.Pr!.Review, first.Link!.ItemState, first.Priority, first.State.Type, first.Labels.Count, first.Cycle!.Number));
        Assert.Equal(DateTimeOffset.Parse("2025-12-31T23:40:00Z", System.Globalization.CultureInfo.InvariantCulture), first.UpdatedAt);
        Assert.Null(list.Rows[2].Project);
        Assert.Null(list.Rows[2].Pr);
    }

    [Fact]
    public async Task The_not_connected_golden_has_no_viewer_and_no_rows()
    {
        var list = (await Client(Ok("linear-issues", "not-connected")).LinearIssues(_api, new LinearIssueQuery("mine"), Token)).Value;

        Assert.Equal((false, null, 0, null), (list.Connected, list.Viewer, list.Rows.Count, list.Feed.LastSyncedAt));
    }

    [Fact]
    public async Task The_feed_goldens_read_failing_with_its_code_and_rereading_with_its_progress()
    {
        var failing = (await Client(Ok("linear-issues", "feed-failing")).LinearIssues(_api, new LinearIssueQuery("mine"), Token)).Value;
        var rereading = (await Client(Ok("linear-issues", "rereading")).LinearIssues(_api, new LinearIssueQuery("mine"), Token)).Value;

        Assert.Equal(("failing", "SND-LINEAR-010"), (failing.Feed.State, failing.Feed.Code));
        Assert.Equal(("rereading", 412, 1000), (rereading.Feed.State, rereading.Feed.Reread!.Done, rereading.Feed.Reread.Total));
    }

    [Fact]
    public async Task The_teams_page_and_the_empty_filter_goldens_read_their_totals()
    {
        var page2 = (await Client(Ok("linear-issues", "teams-page2")).LinearIssues(_api, new LinearIssueQuery("teams"), Token)).Value;
        var empty = (await Client(Ok("linear-issues", "filtered-empty")).LinearIssues(_api, new LinearIssueQuery("mine", State: "x"), Token)).Value;

        Assert.Equal(("teams", 52, null, 2), (page2.Group, page2.Total, page2.Next, page2.Rows.Count));
        Assert.Null(page2.Rows[1].Assignee);
        Assert.Equal((true, 0, 0), (empty.Connected, empty.Rows.Count, empty.Total));
    }

    [Fact]
    public async Task The_full_issue_golden_reads_two_comments_the_second_from_sindri()
    {
        var issue = (await Client(Ok("linear-issue", "full")).LinearIssue(_api, "ENG-12", Token)).Value;

        Assert.Equal(("ENG-12", 2, false, false, true, "Joi Example"), (issue.Key, issue.Comments.Count, issue.Comments[0].Sindri, issue.Removed, issue.Comments[1].Sindri, issue.Viewer!.Name));
        Assert.StartsWith("The sync worker retries", issue.Description, StringComparison.Ordinal);
        Assert.Equal("Sam Lee", issue.Comments[0].Author.Name);
    }

    [Fact]
    public async Task The_removed_golden_reads_removed_true()
    {
        var issue = (await Client(Ok("linear-issue", "removed")).LinearIssue(_api, "ENG-9", Token)).Value;

        Assert.Equal(("ENG-9", true, 0), (issue.Key, issue.Removed, issue.Comments.Count));
    }

    [Fact]
    public async Task The_eng_lookups_golden_reads_five_states_in_position_order_one_inactive_member_and_the_current_cycle()
    {
        var lookups = (await Client(Ok("linear-lookups", "eng")).LinearLookups(_api, "ENG", Token)).Value;

        Assert.Equal(("ENG", 5, 3, 2), (lookups.Team.Key, lookups.States.Count, lookups.Members.Count, lookups.Labels.Count));
        Assert.Equal([0, 1, 2, 3, 4], lookups.States.Select(s => s.Position));
        Assert.Equal("Former Member", Assert.Single(lookups.Members, m => !m.Active).Name);
        Assert.Equal(14, Assert.Single(lookups.Cycles, c => c.Current).Number);
    }

    [Fact]
    public async Task The_not_synced_lookups_golden_has_no_syncedAt_and_empty_lists()
    {
        var lookups = (await Client(Ok("linear-lookups", "not-synced")).LinearLookups(_api, "OPS", Token)).Value;

        Assert.Equal((null, 0, 0, 0, 0), (lookups.SyncedAt, lookups.States.Count, lookups.Members.Count, lookups.Labels.Count, lookups.Cycles.Count));
    }

    [Fact]
    public async Task The_since_goldens_read_three_changes_and_none()
    {
        var three = (await Client(Ok("linear-since", "three")).LinearSince(_api, after: null, Token)).Value;
        var empty = (await Client(Ok("linear-since", "empty")).LinearSince(_api, "lin_3f", Token)).Value;

        Assert.Equal(("lin_000000000000003f", 3), (three.Marker, three.Count));
        Assert.Equal(["assigned", "mentioned", "state"], three.Items.Select(i => i.Kind), StringComparer.Ordinal);
        Assert.Equal(("Todo", "In Progress", null), (three.Items[2].From, three.Items[2].To, three.Items[0].From));
        Assert.Equal(0, empty.Count);
        Assert.Empty(empty.Items);
    }

    [Fact]
    public async Task The_session_goldens_read_with_their_fenced_text_and_the_trust_flag_ignored()
    {
        var list = (await Client(Ok("linear-issues", "session")).LinearIssues(_api, new LinearIssueQuery("mine"), Token)).Value;
        var issue = (await Client(Ok("linear-issue", "session")).LinearIssue(_api, "ENG-12", Token)).Value;

        Assert.StartsWith("<untrusted", list.Rows[0].Title, StringComparison.Ordinal);
        Assert.StartsWith("<untrusted", issue.Comments[0].Body, StringComparison.Ordinal);
        Assert.Equal(2, list.Total);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "linear-issue", "not-found", "SND-LINEAR-005")]
    [InlineData(HttpStatusCode.NotFound, "linear-issue", "session-outside", "SND-LINEAR-005")]
    public async Task A_missing_issue_reads_as_an_error_with_the_linear_code(HttpStatusCode status, string slug, string scenario, string code)
    {
        var result = await Client(new Canned(status, GoldenFiles.Api(slug, scenario))).LinearIssue(_api, "ENG-99", Token);

        Assert.True(result.IsError);
        Assert.Equal(code, result.Error.Code);
    }

    [Fact]
    public async Task A_session_is_refused_the_lookups_and_the_since_strip_with_SND_AUTH_002()
    {
        var lookups = await Client(new Canned(HttpStatusCode.Forbidden, GoldenFiles.Api("linear-lookups", "session-refused"))).LinearLookups(_api, "ENG", Token);
        var since = await Client(new Canned(HttpStatusCode.Forbidden, GoldenFiles.Api("linear-since", "session-refused"))).LinearSince(_api, after: null, Token);

        Assert.Equal(("SND-AUTH-002", "SND-AUTH-002"), (lookups.Error.Code, since.Error.Code));
    }

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Uri> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request.RequestUri!); // why: HttpClient always sets the URI
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
