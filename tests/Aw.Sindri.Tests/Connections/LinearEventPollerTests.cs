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
}
