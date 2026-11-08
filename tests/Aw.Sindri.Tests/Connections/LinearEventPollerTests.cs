using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class LinearEventPollerTests
{
    [Fact]
    public async Task A_label_change_maps_to_the_catalog_event_with_a_stable_delivery_id()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearLabelAdded("ENG-7", "Crash on save", "h-55", "bug", "2026-11-08T10:05:00Z"));
        using var h = PollerHarness.For("linear", provider);
        var ct = TestContext.Current.CancellationToken;

        var labeled = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, cursor: null, ct);
        var again = await h.Poller.Poll(h.Connection, EventKind.LinearIssueLabeled, cursor: null, ct);
        var otherKind = await h.Poller.Poll(h.Connection, EventKind.LinearIssueStateChanged, cursor: null, ct);

        var polled = Assert.Single(labeled.Value.Events);
        Assert.Equal("linear:h-55:label:bug", polled.DeliveryKey);
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
    public async Task A_new_issue_maps_to_the_created_event_with_a_stable_delivery_key()
    {
        var provider = new FakeProvider();
        provider.AddLinearIssue(ProviderFixtures.LinearIssueCreated("ENG-9", "Add retries", "2026-11-08T11:00:00Z"));
        using var h = PollerHarness.For("linear", provider);
        var ct = TestContext.Current.CancellationToken;

        var created = await h.Poller.Poll(h.Connection, EventKind.LinearIssueCreated, cursor: null, ct);
        var again = await h.Poller.Poll(h.Connection, EventKind.LinearIssueCreated, cursor: null, ct);
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

        var added = await h.Poller.Poll(h.Connection, EventKind.LinearCommentAdded, cursor: null, ct);
        var otherKind = await h.Poller.Poll(h.Connection, EventKind.LinearIssueCreated, cursor: "2025-06-01T00:00:00Z", ct);

        var polled = Assert.Single(added.Value.Events);
        Assert.Equal("linear:comment:c-7", polled.DeliveryKey);
        Assert.Equal("ENG-3", polled.Fields["identifier"]);
        Assert.Equal("Fixed in the next build", polled.Fields["body"]);
        Assert.Equal("Ada", polled.Fields["author"]);
        Assert.Equal("2026-11-08T12:00:00Z", added.Value.NextCursor);
        Assert.Empty(otherKind.Value.Events);   // the issue itself was created before that cursor
    }
}
