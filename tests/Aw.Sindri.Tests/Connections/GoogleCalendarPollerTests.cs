using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class GoogleCalendarPollerTests
{
    // The poller's clock stands at KnownInstants.Epoch (2026-01-01T00:00:00Z).
    [Fact]
    public async Task An_event_starting_within_the_window_maps_to_the_catalog_event_and_a_moved_event_gets_a_new_delivery_key()
    {
        var provider = new FakeProvider();
        provider.AddCalendarEvent(ProviderFixtures.CalendarEvent("e1", "Standup", "2026-01-01T00:10:00Z"));
        provider.AddCalendarEvent(ProviderFixtures.CalendarEvent("e2", "Review", "2026-01-01T02:00:00Z"));
        using var h = PollerHarness.For("google", provider);
        var ct = TestContext.Current.CancellationToken;

        var first = await h.Poller.Poll(h.Connection, h.Kind, cursor: null, ct);
        var again = await h.Poller.Poll(h.Connection, h.Kind, cursor: null, ct);
        provider.ClearCalendarEvents();
        provider.AddCalendarEvent(ProviderFixtures.CalendarEvent("e1", "Standup", "2026-01-01T00:12:00Z"));
        var moved = await h.Poller.Poll(h.Connection, h.Kind, cursor: null, ct);

        var polled = Assert.Single(first.Value.Events);
        Assert.Equal("google:e1:2026-01-01T00:10:00Z", polled.DeliveryKey);
        Assert.Equal(EventKind.GoogleCalendarEventStarting, polled.Kind);
        Assert.Equal(KnownInstants.Epoch.AddMinutes(10), polled.OccurredAt);
        Assert.Equal("Standup", polled.Fields["title"]);
        Assert.Equal("https://calendar.google.com/event?eid=e1", polled.Fields["url"]);
        Assert.Equal(polled.DeliveryKey, Assert.Single(again.Value.Events).DeliveryKey);
        Assert.Equal("google:e1:2026-01-01T00:12:00Z", Assert.Single(moved.Value.Events).DeliveryKey);
    }

    [Fact]
    public async Task The_window_is_withinMinutes_from_the_settings()
    {
        var provider = new FakeProvider();
        provider.AddCalendarEvent(ProviderFixtures.CalendarEvent("e1", "Standup", "2026-01-01T00:10:00Z"));
        provider.AddCalendarEvent(ProviderFixtures.CalendarEvent("e2", "Review", "2026-01-01T02:00:00Z"));
        using var narrow = PollerHarness.For("google", provider, new Dictionary<string, string>(StringComparer.Ordinal) { ["withinMinutes"] = "5" });
        using var wide = PollerHarness.For("google", provider, new Dictionary<string, string>(StringComparer.Ordinal) { ["withinMinutes"] = "180" });
        var ct = TestContext.Current.CancellationToken;

        var none = await narrow.Poller.Poll(narrow.Connection, narrow.Kind, cursor: null, ct);
        var both = await wide.Poller.Poll(wide.Connection, wide.Kind, cursor: null, ct);

        Assert.Empty(none.Value.Events);
        Assert.Equal(["google:e1:2026-01-01T00:10:00Z", "google:e2:2026-01-01T02:00:00Z"], both.Value.Events.Select(static e => e.DeliveryKey), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("rateLimitExceeded")]
    [InlineData("userRateLimitExceeded")]
    public async Task A_403_that_names_a_rate_limit_reason_is_rate_limited_and_not_a_sign_in_problem(string reason)
    {
        var provider = new FakeProvider();
        provider.RateLimit(times: 1, retryAfter: null, System.Net.HttpStatusCode.Forbidden, body: "{\"error\":{\"code\":403,\"errors\":[{\"reason\":\"" + reason + "\"}]}}");
        using var h = PollerHarness.For("google", provider);

        var polled = await h.Poller.Poll(h.Connection, h.Kind, cursor: null, TestContext.Current.CancellationToken);

        Assert.True(polled.IsSuccess);
        Assert.True(polled.Value.RateLimited);
    }

    [Fact]
    public async Task A_403_with_another_reason_still_asks_for_a_new_sign_in()
    {
        var provider = new FakeProvider();
        provider.RateLimit(times: 1, retryAfter: null, System.Net.HttpStatusCode.Forbidden, body: "{\"error\":{\"errors\":[{\"reason\":\"forbidden\"}]}}");
        using var h = PollerHarness.For("google", provider);

        var polled = await h.Poller.Poll(h.Connection, h.Kind, cursor: null, TestContext.Current.CancellationToken);

        Assert.Equal("SND-CONN-002", polled.Error.Code);
    }
}
