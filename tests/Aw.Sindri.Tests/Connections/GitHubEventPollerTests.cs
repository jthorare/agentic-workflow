using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class GitHubEventPollerTests
{
    private const string Before = "2026-11-08T09:00:00Z";

    [Fact]
    public async Task PR_merged_and_review_requested_map_to_the_catalog_events_with_stable_delivery_ids()
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrMerged("1001", "acme/app", 42, "Fix login", "ada", "2026-11-08T10:00:00Z"));
        provider.AddGitHubEvent(ProviderFixtures.GitHubReviewRequested("1002", "acme/app", 43, "Add retries", "bob", "ada", "2026-11-08T10:01:00Z"));
        using var h = PollerHarness.For("github", provider);
        var ct = TestContext.Current.CancellationToken;

        var merged = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, ct);
        var requested = await h.Poller.Poll(h.Connection, EventKind.GitHubPrReviewRequested, Before, ct);
        var again = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, ct);
        var overlapped = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, "2026-11-08T10:01:00Z", ct);

        var mergedEvent = Assert.Single(merged.Value.Events);
        Assert.Equal("github:1001", mergedEvent.DeliveryKey);
        Assert.Equal(EventKind.GitHubPrMerged, mergedEvent.Kind);
        Assert.Equal(DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture), mergedEvent.OccurredAt);
        Assert.Equal("acme/app", mergedEvent.Fields["repo"]);
        Assert.Equal("42", mergedEvent.Fields["number"]);
        Assert.Equal("Fix login", mergedEvent.Fields["title"]);
        Assert.Equal("https://github.com/acme/app/pull/42", mergedEvent.Fields["url"]);
        Assert.Equal("ada", mergedEvent.Fields["author"]);
        Assert.Equal("2026-11-08T10:01:00Z", merged.Value.NextCursor);   // the newest created_at, whatever kind it was

        var requestedEvent = Assert.Single(requested.Value.Events);
        Assert.Equal("github:1002", requestedEvent.DeliveryKey);
        Assert.Equal(EventKind.GitHubPrReviewRequested, requestedEvent.Kind);
        Assert.Equal("ada", requestedEvent.Fields["reviewer"]);

        Assert.Equal(mergedEvent.DeliveryKey, Assert.Single(again.Value.Events).DeliveryKey);
        Assert.Equal(mergedEvent.DeliveryKey, Assert.Single(overlapped.Value.Events).DeliveryKey);   // inside the 6 h overlap: a repeat the run-id refusal drops
        Assert.All(provider.Requests, static r => Assert.Equal("Bearer " + PollerHarness.Token, r.Authorization));
    }

    [Fact]
    public async Task A_trimmed_PullRequestEvent_with_action_merged_maps_to_github_pr_merged()
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrTrimmed("3001", "acme/app", 77, "merged", "abc123", "2026-11-08T10:00:00Z"));
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        var e = Assert.Single(polled.Value.Events);
        Assert.Equal(EventKind.GitHubPrMerged, e.Kind);
        Assert.Equal("github:3001", e.DeliveryKey);
        Assert.Equal("acme/app", e.Fields["repo"]);
        Assert.Equal("77", e.Fields["number"]);
    }

    [Fact]
    public async Task A_trimmed_opened_event_still_maps_to_github_pr_opened_and_is_not_a_merge()
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrTrimmed("3002", "acme/app", 78, "opened", "def456", "2026-11-08T10:00:00Z"));
        using var h = PollerHarness.For("github", provider);
        var ct = TestContext.Current.CancellationToken;

        var merged = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, ct);
        var opened = await h.Poller.Poll(h.Connection, EventKind.GitHubPrOpened, Before, ct);

        Assert.Empty(merged.Value.Events);
        Assert.Equal("78", Assert.Single(opened.Value.Events).Fields["number"]);
    }

    [Fact]
    public async Task The_first_poll_is_a_baseline_with_no_events_and_the_newest_created_at_as_the_cursor()
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrMerged("1001", "acme/app", 42, "Fix login", "ada", "2026-11-08T10:00:00Z"));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, cursor: null, TestContext.Current.CancellationToken);

        Assert.True(baseline.IsSuccess);
        Assert.Empty(baseline.Value.Events);
        Assert.Equal("2026-11-08T10:00:00Z", baseline.Value.NextCursor);
    }

    [Fact]
    public async Task A_baseline_on_an_empty_feed_takes_the_cursor_from_the_clock_so_the_next_event_is_not_skipped()
    {
        using var h = PollerHarness.For("github", new FakeProvider());

        var baseline = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, cursor: null, TestContext.Current.CancellationToken);

        Assert.Empty(baseline.Value.Events);
        Assert.Equal("2026-01-01T00:00:00Z", baseline.Value.NextCursor);
    }

    [Fact]
    public async Task A_feed_of_150_events_between_two_polls_is_read_page_by_page_and_returned_oldest_first()
    {
        var provider = new FakeProvider();
        AddMerged(provider, 150);
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        Assert.False(polled.Value.Gap);
        Assert.Equal(150, polled.Value.Events.Count);
        Assert.Equal("github:2001", polled.Value.Events[0].DeliveryKey);
        Assert.Equal("github:2150", polled.Value.Events[^1].DeliveryKey);
        Assert.Equal(2, provider.Requests.Count);
    }

    [Fact]
    public async Task A_feed_longer_than_the_page_cap_returns_the_pages_it_read_and_reports_a_gap()
    {
        var provider = new FakeProvider();
        AddMerged(provider, 350);
        using var h = PollerHarness.For("github", provider, pageCap: 2);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        Assert.True(polled.Value.Gap);
        Assert.Equal(200, polled.Value.Events.Count);
        Assert.Equal("github:2151", polled.Value.Events[0].DeliveryKey);
        Assert.Equal("github:2350", polled.Value.Events[^1].DeliveryKey);
        Assert.Equal(2, provider.Requests.Count);
    }

    [Fact]
    public async Task An_event_that_shows_up_late_inside_the_overlap_is_not_lost_and_one_far_before_it_is_not_returned()
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrMerged("3002", "acme/app", 51, "Far older", "ada", "2026-11-08T03:00:00Z"));
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrMerged("3001", "acme/app", 50, "Late", "ada", "2026-11-08T09:30:00Z"));
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, "2026-11-08T10:00:00Z", TestContext.Current.CancellationToken);

        Assert.Equal("github:3001", Assert.Single(polled.Value.Events).DeliveryKey);
    }

    [Fact]
    public async Task A_403_with_no_requests_left_is_rate_limited_and_waits_until_the_reset()
    {
        var provider = new FakeProvider();
        var reset = KnownInstants.Epoch.AddSeconds(120).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        provider.RateLimit(times: 1, retryAfter: null, HttpStatusCode.Forbidden, new Dictionary<string, string>(StringComparer.Ordinal) { ["x-ratelimit-remaining"] = "0", ["x-ratelimit-reset"] = reset });
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        Assert.True(polled.IsSuccess);
        Assert.True(polled.Value.RateLimited);
        Assert.Equal(TimeSpan.FromSeconds(120), polled.Value.RetryAfter);
        Assert.Equal(Before, polled.Value.NextCursor);
    }

    [Fact]
    public async Task A_403_without_a_rate_limit_header_still_asks_for_a_new_sign_in()
    {
        var provider = new FakeProvider();
        provider.RateLimit(times: 1, retryAfter: null, HttpStatusCode.Forbidden);
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        Assert.True(polled.IsError);
        Assert.Equal("SND-CONN-002", polled.Error.Code);
    }

    [Fact]
    public async Task The_same_event_seen_twice_in_one_poll_is_returned_once()
    {
        var provider = new FakeProvider();
        var json = ProviderFixtures.GitHubPrMerged("4001", "acme/app", 60, "Twice", "ada", "2026-11-08T10:00:00Z");
        provider.AddGitHubEvent(json);
        provider.AddGitHubEvent(json);
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        Assert.Equal("github:4001", Assert.Single(polled.Value.Events).DeliveryKey);
    }

    [Fact]
    public async Task A_400_is_a_provider_error_and_never_a_rejected_cursor_because_GitHub_gets_no_cursor()
    {
        var provider = new FakeProvider();
        provider.RateLimit(times: 1, retryAfter: null, HttpStatusCode.BadRequest);
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        Assert.Equal("SND-CONN-012", polled.Error.Code);
    }

    private static void AddMerged(FakeProvider provider, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            var id = (2000 + i).ToString(CultureInfo.InvariantCulture);
            var created = DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture).AddSeconds(i).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            provider.AddGitHubEvent(ProviderFixtures.GitHubPrMerged(id, "acme/app", i, "t" + id, "ada", created));
        }
    }
}
