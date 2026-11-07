using System.Net;
using System.Text;
using System.Text.Json.Nodes;
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

    // A golden as served: a ProblemDetails keeps the status it names, anything else is a 200.
    private static Canned Served(string slug, string scenario)
    {
        var body = GoldenFiles.Api(slug, scenario);
        var status = JsonNode.Parse(body) is JsonObject { } json && json["traceId"] is not null && json["status"] is JsonValue { } code ? (HttpStatusCode)(int)code : HttpStatusCode.OK;
        return new(status, body);
    }

    private static readonly JsonObject _noFields = [];

    private static JsonObject Fields(string name, JsonNode? value) => new() { [name] = value };

    [Theory]
    [InlineData("writable", true, null)]
    [InlineData("no-write-key", false, "SND-LINEAR-002")]
    [InlineData("team-not-picked", false, "SND-LINEAR-003")]
    [InlineData("session-write-refused", false, "SND-AUTH-002")]
    [InlineData("full", true, null)]
    [InlineData("removed", false, null)]
    [InlineData("session", false, "SND-AUTH-002")]
    public async Task The_issue_goldens_carry_whether_the_sheet_may_write(string scenario, bool allowed, string? code)
    {
        var issue = (await Client(Ok("linear-issue", scenario)).LinearIssue(_api, "ENG-12", Token)).Value;

        Assert.Equal((allowed, code), (issue.Write.Allowed, issue.Write.Code));
    }

    [Fact]
    public async Task LinearChange_posts_the_opened_and_set_fields_to_the_change_route()
    {
        var canned = Served("linear-change", "applied");

        _ = await Client(canned).LinearChange(_api, "ENG-12", Fields("stateId", "s1"), Fields("stateId", "s2"), Token);
        _ = await Client(canned).LinearChange(_api, "a/b", Fields("assigneeId", "u1"), Fields("assigneeId", value: null), Token);

        Assert.Equal([HttpMethod.Post, HttpMethod.Post], canned.Methods);
        Assert.Equal(["/sindri/linear/issues/ENG-12/change", "/sindri/linear/issues/a%2Fb/change"], canned.Seen.Select(u => u.AbsolutePath), StringComparer.Ordinal);
        Assert.Equal(["""{"opened":{"stateId":"s1"},"set":{"stateId":"s2"}}""", """{"opened":{"assigneeId":"u1"},"set":{"assigneeId":null}}"""], canned.Bodies);
    }

    [Theory]
    [InlineData("applied", null, null)]
    [InlineData("applied-store-failed", "SND-LINEAR-013", null)]
    [InlineData("already-applied", null, true)]
    public async Task LinearChange_reads_an_applied_edit_with_the_re_read_issue(string scenario, string? warning, bool? already)
    {
        var result = await Client(Served("linear-change", scenario)).LinearChange(_api, "ENG-12", _noFields, _noFields, Token);

        var applied = Assert.IsType<LinearChangeApplied>(result.Value);
        Assert.Equal(("applied", warning, already, "ENG-12", "In Review"), (applied.Outcome, applied.Warning, applied.AlreadyApplied, applied.Issue.Key, applied.Issue.State.Name));
        Assert.True(applied.Issue.Write.Allowed);
    }

    [Fact]
    public async Task LinearChange_reads_a_409_as_a_conflict_with_the_fields_and_the_live_issue()
    {
        var result = await Client(Served("linear-change", "conflict")).LinearChange(_api, "ENG-12", _noFields, _noFields, Token);

        var conflict = Assert.IsType<LinearChangeConflict>(result.Value);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T10:01:00Z", System.Globalization.CultureInfo.InvariantCulture), conflict.ChangedAt);
        var field = Assert.Single(conflict.Fields);
        Assert.Equal(("stateId", "00000000-0000-4000-8000-000000000052", "00000000-0000-4000-8000-000000000055", "Done"), (field.Field, field.Opened.GetString(), field.Linear.GetString(), conflict.Issue.State.Name));
    }

    [Fact]
    public async Task LinearChange_reads_a_504_011_as_unknown_and_a_429_012_as_rate_limited_with_its_time()
    {
        var unknown = await Client(Served("linear-change", "unknown")).LinearChange(_api, "ENG-12", _noFields, _noFields, Token);
        var limited = await Client(Served("linear-change", "rate-limited")).LinearChange(_api, "ENG-12", _noFields, _noFields, Token);

        Assert.Equal(new LinearWriteUnknown(ClientId: null), unknown.Value);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T10:06:00Z", System.Globalization.CultureInfo.InvariantCulture), Assert.IsType<LinearWriteRateLimited>(limited.Value).RetryAfter);
    }

    [Theory]
    [InlineData("no-write-key", "SND-LINEAR-002")]
    [InlineData("team-not-picked", "SND-LINEAR-003")]
    [InlineData("bad-value", "SND-LINEAR-006")]
    [InlineData("session-refused", "SND-AUTH-002")]
    public async Task LinearChange_hands_any_other_refusal_back_as_the_error(string scenario, string code)
    {
        var result = await Client(Served("linear-change", scenario)).LinearChange(_api, "ENG-12", _noFields, _noFields, Token);

        Assert.True(result.IsError);
        Assert.Equal(code, result.Error.Code);
        Assert.False(string.IsNullOrEmpty(result.Error.Message));
    }

    [Fact]
    public async Task LinearComment_posts_the_client_id_and_text_and_reads_the_comment_and_whether_it_was_slow()
    {
        var canned = Served("linear-comment", "applied");
        var slow = await Client(Served("linear-comment", "applied-slow")).LinearComment(_api, "ENG-12", "c-1", "hi", Token);

        var posted = Assert.IsType<LinearCommentPosted>((await Client(canned).LinearComment(_api, "ENG-12", "c-1", "Say \"hi\"", Token)).Value);

        Assert.Equal(HttpMethod.Post, canned.Methods[0]);
        Assert.Equal("/sindri/linear/issues/ENG-12/comments", canned.Seen[0].AbsolutePath);
        Assert.Equal("""{"clientId":"c-1","body":"Say \"hi\""}""", canned.Bodies[0], StringComparer.Ordinal);
        Assert.Equal((false, "5d0a4f6e-3b1c-4c0e-9a57-2f1d8c9b7e01", true, "Joi Example"), (posted.Slow, posted.ClientId, posted.Comment.Sindri, posted.Comment.Author.Name));
        Assert.True(Assert.IsType<LinearCommentPosted>(slow.Value).Slow);
    }

    [Fact]
    public async Task LinearComment_reads_an_unknown_outcome_with_its_client_id_and_hands_a_session_refusal_back()
    {
        var unknown = await Client(Served("linear-comment", "unknown")).LinearComment(_api, "ENG-12", "c-1", "hi", Token);
        var refused = await Client(Served("linear-comment", "session-refused")).LinearComment(_api, "ENG-12", "c-1", "hi", Token);

        Assert.Equal(new LinearWriteUnknown("5d0a4f6e-3b1c-4c0e-9a57-2f1d8c9b7e01"), unknown.Value);
        Assert.Equal("SND-AUTH-002", refused.Error.Code);
    }

    [Fact]
    public async Task LinearCreate_posts_to_the_issues_route_and_leaves_the_unset_fields_out()
    {
        var canned = Served("linear-create", "applied");

        var created = Assert.IsType<LinearCreated>((await Client(canned).LinearCreate(_api, new LinearCreateBody("c-1", "ENG", "T"), Token)).Value);
        _ = await Client(canned).LinearCreate(_api, new LinearCreateBody("c-2", "ENG", "T", "d", "s", AssigneeId: null, 0, "p", "cy", ["l1"], "ENG-1"), Token);

        Assert.Equal(HttpMethod.Post, canned.Methods[0]);
        Assert.Equal("/sindri/linear/issues", canned.Seen[0].AbsolutePath);
        Assert.Equal("""{"clientId":"c-1","team":"ENG","title":"T"}""", canned.Bodies[0]);
        Assert.Equal("""{"clientId":"c-2","team":"ENG","title":"T","description":"d","stateId":"s","priority":0,"projectId":"p","cycleId":"cy","labelIds":["l1"],"parentKey":"ENG-1"}""", canned.Bodies[1]);
        Assert.Equal(("ENG-58", "5d0a4f6e-3b1c-4c0e-9a57-2f1d8c9b7e01"), (created.Issue.Key, created.ClientId));
    }

    [Fact]
    public async Task LinearCreate_reads_unknown_with_its_client_id_and_hands_a_bad_value_back_as_the_error()
    {
        var unknown = await Client(Served("linear-create", "unknown")).LinearCreate(_api, new LinearCreateBody("c-1", "ENG", "T"), Token);
        var bad = await Client(Served("linear-create", "bad-value")).LinearCreate(_api, new LinearCreateBody("c-1", "ENG", "T"), Token);
        var refused = await Client(Served("linear-create", "session-refused")).LinearCreate(_api, new LinearCreateBody("c-1", "ENG", "T"), Token);

        Assert.Equal(new LinearWriteUnknown("5d0a4f6e-3b1c-4c0e-9a57-2f1d8c9b7e01"), unknown.Value);
        Assert.Equal(("SND-LINEAR-006", "SND-AUTH-002"), (bad.Error.Code, refused.Error.Code));
    }

    [Theory]
    [InlineData("connected", "resolves", "resolves", 2, false)]
    [InlineData("not-connected", "notSet", "notSet", 0, false)]
    [InlineData("read-only", "resolves", "notSet", 2, false)]
    [InlineData("scope-unknown", "resolves", "scopeUnknown", 2, false)]
    [InlineData("pending", "resolves", "notSet", 2, true)]
    public async Task LinearConnection_gets_the_route_and_reads_both_keys_the_teams_and_pending(string scenario, string read, string write, int teams, bool pending)
    {
        var canned = Served("linear-connection", scenario);

        var connection = (await Client(canned).LinearConnection(_api, Token)).Value;

        Assert.Equal((HttpMethod.Get, "/sindri/linear/connection"), (canned.Methods[0], canned.Seen[0].AbsolutePath));
        Assert.Equal((read, write, teams, pending), (connection.Read.State, connection.Write.State, connection.Teams.Count, connection.Pending));
        Assert.Equal(scenario == "not-connected", connection.Read.CheckedAt is null);
    }

    [Fact]
    public async Task LinearConnection_is_refused_a_session()
    {
        var result = await Client(Served("linear-connection", "session-refused")).LinearConnection(_api, Token);

        Assert.Equal("SND-AUTH-002", result.Error.Code);
    }

    [Fact]
    public async Task LinearConnectionTeams_posts_the_key_and_reads_the_teams_or_the_refusal()
    {
        var canned = Served("linear-connection-teams", "two-teams");

        var teams = (await Client(canned).LinearConnectionTeams(_api, "lin_api_read", Token)).Value;
        var refused = await Client(Served("linear-connection-teams", "refused")).LinearConnectionTeams(_api, "k", Token);
        var unreachable = await Client(Served("linear-connection-teams", "unreachable")).LinearConnectionTeams(_api, "k", Token);

        Assert.Equal((HttpMethod.Post, "/sindri/linear/connection/teams", """{"key":"lin_api_read"}"""), (canned.Methods[0], canned.Seen[0].AbsolutePath, canned.Bodies[0]));
        Assert.Equal(["ENG", "OPS"], teams.Teams.Select(t => t.Key), StringComparer.Ordinal);
        Assert.Equal(("SND-LINEAR-004", "SND-LINEAR-010"), (refused.Error.Code, unreachable.Error.Code));
    }

    [Fact]
    public async Task LinearConnect_posts_a_read_key_with_its_teams_and_a_write_key_without()
    {
        var read = Served("linear-connect", "read-saved-approved");
        var write = Served("linear-connect", "write-saved");

        var saved = (await Client(read).LinearConnect(_api, "read", "k1", ["ENG", "OPS"], Token)).Value;
        var wrote = (await Client(write).LinearConnect(_api, "write", "k2", teams: null, Token)).Value;

        Assert.Equal((HttpMethod.Post, "/sindri/linear/connection"), (read.Methods[0], read.Seen[0].AbsolutePath));
        Assert.Equal("""{"kind":"read","key":"k1","teams":["ENG","OPS"]}""", read.Bodies[0]);
        Assert.Equal("""{"kind":"write","key":"k2"}""", write.Bodies[0]);
        Assert.Equal((true, true, null), (saved.Saved, saved.Approved, saved.Write));
        Assert.Equal("resolves", wrote.Write!.State);
    }

    [Fact]
    public async Task LinearConnect_reads_a_save_that_waits_on_other_profile_changes_and_a_write_key_of_unknown_scope()
    {
        var waiting = (await Client(Served("linear-connect", "read-saved-waiting")).LinearConnect(_api, "read", "k", ["ENG"], Token)).Value;
        var scope = (await Client(Served("linear-connect", "write-scope-unknown")).LinearConnect(_api, "write", "k", teams: null, Token)).Value;

        Assert.Equal((true, false, 64), (waiting.Saved, waiting.Approved, waiting.ProfileHash.Length));
        Assert.Equal("scopeUnknown", scope.Write!.State);
    }

    [Theory]
    [InlineData("same-as-read", "SND-LINEAR-001")]
    [InlineData("no-keychain", "SND-LINEAR-014")]
    [InlineData("no-teams", "SND-LINEAR-003")]
    [InlineData("session-refused", "SND-AUTH-002")]
    public async Task LinearConnect_hands_its_refusals_back_as_the_error(string scenario, string code)
    {
        var result = await Client(Served("linear-connect", scenario)).LinearConnect(_api, "write", "k", teams: null, Token);

        Assert.Equal(code, result.Error.Code);
    }

    [Fact]
    public async Task LinearDisconnectWrite_deletes_the_write_key_and_reads_the_answer()
    {
        var canned = Served("linear-disconnect-write", "removed");

        var removed = (await Client(canned).LinearDisconnectWrite(_api, Token)).Value;

        Assert.Equal((HttpMethod.Delete, "/sindri/linear/connection/write", null), (canned.Methods[0], canned.Seen[0].AbsolutePath, canned.Bodies[0]));
        Assert.Equal((true, true), (removed.Removed, removed.Approved));
    }

    [Fact]
    public void The_write_event_and_the_unknown_outcome_row_read_through_the_apps_json_context()
    {
        var evt = Aw.Kernel.Json.AwJson.Deserialize<LinearWriteEvent>(Encoding.UTF8.GetBytes("""{"key":"ENG-12","team":"ENG","clientId":null,"attempt":2,"write":"edit","outcome":"failed","code":"SND-LINEAR-011"}"""), SindriApiClient.Json).Value;
        var row = Aw.Kernel.Json.AwJson.Deserialize<LinearWriteUnknownRow>(Encoding.UTF8.GetBytes("""{"kind":"linear.write.unknown","key":null,"team":"ENG","clientId":"c-1","write":"create","at":"2026-01-01T10:05:00.0000000Z","text":"A new ENG issue from 10:05 may have been created"}"""), SindriApiClient.Json).Value;

        Assert.Equal(("ENG-12", null, 2, "edit", "failed", "SND-LINEAR-011"), (evt.Key, evt.ClientId, evt.Attempt, evt.Write, evt.Outcome, evt.Code));
        Assert.Equal((null, "create"), (row.Key, row.Write));
    }

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Uri> Seen { get; } = [];

        public List<HttpMethod> Methods { get; } = [];

        public List<string?> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request.RequestUri!); // why: HttpClient always sets the URI
            Methods.Add(request.Method);
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
