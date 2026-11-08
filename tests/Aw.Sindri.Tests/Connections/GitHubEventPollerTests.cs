using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class GitHubEventPollerTests
{
    [Fact]
    public async Task PR_merged_and_review_requested_map_to_the_catalog_events_with_stable_delivery_ids()
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrMerged("1001", "acme/app", 42, "Fix login", "ada", "2026-11-08T10:00:00Z"));
        provider.AddGitHubEvent(ProviderFixtures.GitHubReviewRequested("1002", "acme/app", 43, "Add retries", "bob", "ada", "2026-11-08T10:01:00Z"));
        using var h = PollerHarness.For("github", provider);
        var ct = TestContext.Current.CancellationToken;

        var merged = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, cursor: null, ct);
        var requested = await h.Poller.Poll(h.Connection, EventKind.GitHubPrReviewRequested, cursor: null, ct);
        var again = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, cursor: null, ct);
        var caughtUp = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, cursor: "1002", ct);

        var mergedEvent = Assert.Single(merged.Value.Events);
        Assert.Equal("github:1001", mergedEvent.DeliveryKey);
        Assert.Equal(EventKind.GitHubPrMerged, mergedEvent.Kind);
        Assert.Equal(DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture), mergedEvent.OccurredAt);
        Assert.Equal("acme/app", mergedEvent.Fields["repo"]);
        Assert.Equal("42", mergedEvent.Fields["number"]);
        Assert.Equal("Fix login", mergedEvent.Fields["title"]);
        Assert.Equal("https://github.com/acme/app/pull/42", mergedEvent.Fields["url"]);
        Assert.Equal("ada", mergedEvent.Fields["author"]);
        Assert.Equal("1002", merged.Value.NextCursor);

        var requestedEvent = Assert.Single(requested.Value.Events);
        Assert.Equal("github:1002", requestedEvent.DeliveryKey);
        Assert.Equal(EventKind.GitHubPrReviewRequested, requestedEvent.Kind);
        Assert.Equal("ada", requestedEvent.Fields["reviewer"]);

        Assert.Equal(mergedEvent.DeliveryKey, Assert.Single(again.Value.Events).DeliveryKey);
        Assert.Empty(caughtUp.Value.Events);
        Assert.Equal("1002", caughtUp.Value.NextCursor);
        Assert.All(provider.Requests, static r => Assert.Equal("Bearer " + PollerHarness.Token, r.Authorization));
    }
}
