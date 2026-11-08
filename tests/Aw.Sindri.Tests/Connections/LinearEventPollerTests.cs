using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class LinearEventPollerTests
{
    private const string Before = "2026-11-08T09:00:00Z";

    [Fact]
    public async Task A_label_change_maps_to_the_catalog_event_with_a_stable_delivery_id()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearLabelAdded("ENG-7", "Crash on save", "h-55", "bug", "lbl-1", "2026-11-08T10:05:00Z"));
        using var h = PollerHarness.For("linear", provider);
        var ct = TestContext.Current.CancellationToken;

        var labeled = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, Before, ct);
        var again = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, Before, ct);
        var otherKind = await h.Poller.Poll(h.Connection, EventKind.LinearIssueStateChanged, Before, ct);

        var polled = Assert.Single(labeled.Value.Events);
        Assert.Equal("linear:h-55:label:lbl-1", polled.DeliveryKey);   // the label's id, not its name
        Assert.Equal(EventKind.LinearIssueLabeled, polled.Kind);
        Assert.Equal("ENG-7", polled.Fields["identifier"]);
        Assert.Equal("Crash on save", polled.Fields["title"]);
        Assert.Equal("https://linear.app/acme/issue/ENG-7", polled.Fields["url"]);
        Assert.Equal("bug", polled.Fields["label"]);
        Assert.Equal("2026-11-08T10:05:00Z", labeled.Value.NextCursor);
        Assert.Equal(polled.DeliveryKey, Assert.Single(again.Value.Events).DeliveryKey);
        Assert.Empty(otherKind.Value.Events);
        Assert.All(provider.Requests, static r =>
        {
            Assert.Equal("POST", r.Method);
            Assert.Equal("Bearer " + PollerHarness.Token, r.Authorization);
        });
    }

    [Fact]
    public async Task A_state_change_is_keyed_by_the_state_id()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearStateChanged("ENG-8", "Slow list", "h-60", "s-2", "Done", "2026-11-08T10:06:00Z"));
        using var h = PollerHarness.For("linear", provider);

        var changed = await h.Poller.Poll(h.Connection, EventKind.LinearIssueStateChanged, Before, TestContext.Current.CancellationToken);

        var polled = Assert.Single(changed.Value.Events);
        Assert.Equal("linear:h-60:state:s-2", polled.DeliveryKey);
        Assert.Equal("Done", polled.Fields["to"]);
        Assert.Equal("Todo", polled.Fields["from"]);
    }

    [Fact]
    public async Task The_first_poll_is_a_baseline_with_no_events_and_the_newest_time_as_the_cursor()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearLabelAdded("ENG-7", "Crash on save", "h-55", "bug", "lbl-1", "2026-11-08T10:05:00Z"));
        using var h = PollerHarness.For("linear", provider);
        using var empty = PollerHarness.For("linear", new FakeProvider());
        var ct = TestContext.Current.CancellationToken;

        var baseline = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, cursor: null, ct);
        var onEmpty = await empty.Poller.Poll(empty.Connection, EventKind.LinearIssueLabeled, cursor: null, ct);

        Assert.Empty(baseline.Value.Events);
        Assert.Equal("2026-11-08T10:05:00Z", baseline.Value.NextCursor);
        Assert.Empty(onEmpty.Value.Events);
        Assert.Equal("2026-01-01T00:00:00Z", onEmpty.Value.NextCursor);
    }

    [Fact]
    public async Task A_new_issue_maps_to_the_created_event_with_a_stable_delivery_key()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearIssueCreated("ENG-9", "Add retries", "2026-11-08T11:00:00Z"));
        using var h = PollerHarness.For("linear", provider);
        var ct = TestContext.Current.CancellationToken;

        var created = await h.Poller.Poll(h.Connection, EventKind.LinearIssueCreated, Before, ct);
        var again = await h.Poller.Poll(h.Connection, EventKind.LinearIssueCreated, Before, ct);
        var caughtUp = await h.Poller.Poll(h.Connection, EventKind.LinearIssueCreated, cursor: "2026-11-08T11:00:00Z", ct);

        var polled = Assert.Single(created.Value.Events);
        Assert.Equal("linear:issue:ENG-9:created", polled.DeliveryKey);
        Assert.Equal(EventKind.LinearIssueCreated, polled.Kind);
        Assert.Equal("ENG-9", polled.Fields["identifier"]);
        Assert.Equal("Add retries", polled.Fields["title"]);
        Assert.Equal("https://linear.app/acme/issue/ENG-9", polled.Fields["url"]);
        Assert.Equal("2026-11-08T11:00:00Z", created.Value.NextCursor);
        Assert.Equal(polled.DeliveryKey, Assert.Single(again.Value.Events).DeliveryKey);
        Assert.Empty(caughtUp.Value.Events);
    }

    [Fact]
    public async Task A_comment_maps_to_the_comment_added_event_with_a_stable_delivery_key()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearComment("ENG-3", "Crash on save", "c-7", "Fixed in the next build", "Ada", "2026-11-08T12:00:00Z"));
        using var h = PollerHarness.For("linear", provider);
        var ct = TestContext.Current.CancellationToken;

        var added = await h.Poller.Poll(h.Connection, EventKind.LinearCommentAdded, Before, ct);
        var otherKind = await h.Poller.Poll(h.Connection, EventKind.LinearIssueCreated, cursor: "2025-06-01T00:00:00Z", ct);

        var polled = Assert.Single(added.Value.Events);
        Assert.Equal("linear:comment:c-7", polled.DeliveryKey);
        Assert.Equal("ENG-3", polled.Fields["identifier"]);
        Assert.Equal("Fixed in the next build", polled.Fields["body"]);
        Assert.Equal("Ada", polled.Fields["author"]);
        Assert.Equal("2026-11-08T12:00:00Z", added.Value.NextCursor);
        Assert.Empty(otherKind.Value.Events);   // the issue itself was created before that cursor
    }

    [Fact]
    public async Task A_feed_of_120_issues_between_two_polls_are_read_page_by_page_and_a_page_cap_reports_a_gap()
    {
        var provider = new FakeProvider();
        for (var i = 1; i <= 120; i++)
        {
            var at = DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture).AddSeconds(i).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            provider.AddLinearIssue(ProviderFixtures.LinearLabelAdded("ENG-" + i.ToString(CultureInfo.InvariantCulture), "t", "h-" + i.ToString(CultureInfo.InvariantCulture), "bug", "lbl-1", at));
        }

        using var all = PollerHarness.For("linear", provider);
        using var capped = PollerHarness.For("linear", provider, pageCap: 2);
        var ct = TestContext.Current.CancellationToken;

        var whole = await all.Poller.Poll(all.Connection, EventKind.LinearIssueLabeled, Before, ct);
        var cut = await capped.Poller.Poll(capped.Connection, EventKind.LinearIssueLabeled, Before, ct);

        Assert.False(whole.Value.Gap);
        Assert.Equal(120, whole.Value.Events.Count);
        Assert.Equal("linear:h-1:label:lbl-1", whole.Value.Events[0].DeliveryKey);
        Assert.Equal("linear:h-120:label:lbl-1", whole.Value.Events[^1].DeliveryKey);
        Assert.True(cut.Value.Gap);
        Assert.Equal(100, cut.Value.Events.Count);
    }

    [Fact]
    public async Task A_400_that_says_RATELIMITED_is_rate_limited_and_not_a_rejected_cursor()
    {
        var provider = new FakeProvider();
        provider.RateLimit(times: 1, retryAfter: null, HttpStatusCode.BadRequest, body: """{"errors":[{"message":"Rate limit exceeded","extensions":{"code":"RATELIMITED"}}]}""");
        using var h = PollerHarness.For("linear", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, Before, TestContext.Current.CancellationToken);

        Assert.True(polled.IsSuccess);
        Assert.True(polled.Value.RateLimited);
        Assert.Equal(Before, polled.Value.NextCursor);
    }

    [Fact]
    public async Task A_personal_API_key_goes_bare_in_Authorization_and_an_OAuth_token_keeps_Bearer()
    {
        var provider = new FakeProvider();
        using var h = PollerHarness.For("linear", provider);
        var ct = TestContext.Current.CancellationToken;

        _ = await h.Poller.Poll(h.Connection with { Kind = ConnectionKind.Rest }, EventKind.LinearIssueLabeled, Before, ct);
        _ = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, Before, ct);

        Assert.Equal([PollerHarness.Token, "Bearer " + PollerHarness.Token], provider.Requests.Select(static r => r.Authorization), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_comment_list_with_a_next_page_reports_a_gap_and_one_without_does_not()
    {
        var first = DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture);
        var more = new FakeProvider();
        more.AddLinearIssue(ProviderFixtures.LinearManyComments("ENG-4", 10, hasNextPage: true, first));
        var whole = new FakeProvider();
        whole.AddLinearIssue(ProviderFixtures.LinearManyComments("ENG-4", 3, hasNextPage: false, first));
        using var cut = PollerHarness.For("linear", more);
        using var done = PollerHarness.For("linear", whole);
        var ct = TestContext.Current.CancellationToken;

        var capped = await cut.Poller.Poll(cut.Connection, EventKind.LinearCommentAdded, Before, ct);
        var complete = await done.Poller.Poll(done.Connection, EventKind.LinearCommentAdded, Before, ct);

        Assert.True(capped.Value.Gap);
        Assert.Equal(10, capped.Value.Events.Count);
        Assert.False(complete.Value.Gap);
        Assert.Equal(3, complete.Value.Events.Count);
    }

    [Fact]
    public async Task A_history_list_with_a_next_page_reports_a_gap()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearManyLabels("ENG-5", 25, hasNextPage: true, DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture)));
        using var h = PollerHarness.For("linear", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, Before, TestContext.Current.CancellationToken);

        Assert.True(polled.Value.Gap);
        Assert.Equal(25, polled.Value.Events.Count);
    }

    [Fact]
    public async Task Eleven_old_comments_and_1_new_one_give_1_event_and_no_gap_because_the_query_filters_by_the_cursor()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearManyComments("ENG-4", 12, hasNextPage: false, DateTimeOffset.Parse("2026-11-08T08:00:00Z", CultureInfo.InvariantCulture)));
        using var h = PollerHarness.For("linear", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.LinearCommentAdded, "2026-11-08T08:00:11Z", TestContext.Current.CancellationToken);

        Assert.Equal("linear:comment:c-12", Assert.Single(polled.Value.Events).DeliveryKey);
        Assert.False(polled.Value.Gap);
    }

    [Fact]
    public async Task Twenty_six_old_history_entries_and_1_new_one_give_1_event_and_no_gap()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearManyLabels("ENG-5", 27, hasNextPage: false, DateTimeOffset.Parse("2026-11-08T08:00:00Z", CultureInfo.InvariantCulture)));
        using var h = PollerHarness.For("linear", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, "2026-11-08T08:00:26Z", TestContext.Current.CancellationToken);

        Assert.Equal("linear:h-27:label:lbl-27", Assert.Single(polled.Value.Events).DeliveryKey);
        Assert.False(polled.Value.Gap);
    }

    [Fact]
    public async Task Twenty_five_new_history_entries_with_a_next_page_give_25_events_and_a_gap()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearManyLabels("ENG-6", 25, hasNextPage: true, DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture)));
        using var h = PollerHarness.For("linear", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, Before, TestContext.Current.CancellationToken);

        Assert.Equal(25, polled.Value.Events.Count);
        Assert.True(polled.Value.Gap);   // every returned entry is newer than the cursor and more exist
    }

    [Fact]
    public async Task History_served_oldest_first_with_a_next_page_reports_a_gap_that_names_the_order()
    {
        var provider = new FakeProvider();
        provider.LinearHistoryOldestFirst();
        provider.AddLinearIssue(ProviderFixtures.LinearManyLabels("ENG-7", 26, hasNextPage: false, DateTimeOffset.Parse("2026-11-08T08:00:00Z", CultureInfo.InvariantCulture)));
        using var h = PollerHarness.For("linear", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, "2026-11-08T08:00:10Z", TestContext.Current.CancellationToken);

        Assert.True(polled.Value.Gap);
        Assert.Contains("history order", polled.Value.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_cursor_goes_to_the_query_as_a_variable_that_both_nested_lists_and_the_issues_filter_use()
    {
        var provider = new FakeProvider();
        using var h = PollerHarness.For("linear", provider);

        _ = await h.Poller.Poll(h.Connection, EventKind.LinearCommentAdded, "2026-11-08T08:00:11Z", TestContext.Current.CancellationToken);

        using var request = System.Text.Json.JsonDocument.Parse(Assert.Single(provider.Requests).Body);
        var query = request.RootElement.GetProperty("query").GetString();
        Assert.Contains("$since: DateTimeOrDuration", query, StringComparison.Ordinal);
        Assert.Contains("comments(first: 10, filter: { createdAt: { gt: $since } })", query, StringComparison.Ordinal);
        Assert.Contains("history(first: 25, orderBy: createdAt)", query, StringComparison.Ordinal);   // history takes no filter argument
        Assert.DoesNotContain("history(first: 25, filter", query, StringComparison.Ordinal);
        Assert.Contains("filter: { updatedAt: { gt: $since } }", query, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-11-08", query, StringComparison.Ordinal);   // the instant travels in variables, never spliced into the text
        Assert.StartsWith("2026-11-08T08:00:11", request.RootElement.GetProperty("variables").GetProperty("since").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_baseline_query_has_no_since_variable_and_no_nested_filter()
    {
        var provider = new FakeProvider();
        using var h = PollerHarness.For("linear", provider);

        _ = await h.Poller.Poll(h.Connection, EventKind.LinearCommentAdded, cursor: null, TestContext.Current.CancellationToken);

        using var request = System.Text.Json.JsonDocument.Parse(Assert.Single(provider.Requests).Body);
        Assert.DoesNotContain("$since", request.RootElement.GetProperty("query").GetString(), StringComparison.Ordinal);
    }
}
