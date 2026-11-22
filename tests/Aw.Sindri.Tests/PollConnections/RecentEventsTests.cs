using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Features.PollConnections;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.PollConnections;

/// <summary>
/// The events seen on this box, kept for the trigger step's "Recent events" and a test run's input (#3201, spec §4.3, §9 E6): the last 20 per
/// (connection, kind), newest first by the provider's time, one row per delivery id.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RecentEventsTests
{
    private static readonly ConnectionName _work = ConnectionSamples.Name("github", "work");
    private static readonly ConnectionName _home = ConnectionSamples.Name("github", "home");
    private static readonly EventKind _merged = EventKind.GitHubPrMerged;
    private static readonly DateTimeOffset _t0 = DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture);

    private static PolledEvent Event(string key, int minutes, EventKind? kind = null, string title = "Fix login") =>
        new(key, kind ?? _merged, _t0.AddMinutes(minutes), new Dictionary<string, string>(StringComparer.Ordinal) { ["title"] = title });

    [Fact]
    public void The_newest_events_come_first_by_the_providers_time()
    {
        var recent = new RecentEvents();
        recent.Record(_work, Event("github:2", minutes: 2));
        recent.Record(_work, Event("github:1", minutes: 1));
        recent.Record(_work, Event("github:3", minutes: 3));

        var listed = recent.Newest(_merged, _work, max: 10);

        Assert.Equal(["github:3", "github:2", "github:1"], listed.Select(static r => r.Event.DeliveryKey), StringComparer.Ordinal);
        Assert.All(listed, static r => Assert.Equal(_work, r.Connection));
    }

    [Fact]
    public void The_same_delivery_recorded_twice_keeps_one_row_with_the_last_fields()
    {
        var recent = new RecentEvents();
        recent.Record(_work, Event("github:1", minutes: 1, title: "old"));
        recent.Record(_work, Event("github:1", minutes: 1, title: "new"));   // the 6 h overlap reads it again

        var row = Assert.Single(recent.Newest(_merged, _work, max: 10));

        Assert.Equal("new", row.Event.Fields["title"]);
    }

    [Fact]
    public void The_21st_event_of_a_kind_drops_the_oldest()
    {
        var recent = new RecentEvents();
        for (var i = 1; i <= RecentEvents.PerKind + 1; i++)
        {
            recent.Record(_work, Event("github:" + i.ToString(CultureInfo.InvariantCulture), minutes: i));
        }

        var listed = recent.Newest(_merged, _work, max: 50);

        Assert.Equal(20, RecentEvents.PerKind);
        Assert.Equal(RecentEvents.PerKind, listed.Count);
        Assert.Equal("github:21", listed[0].Event.DeliveryKey);
        Assert.DoesNotContain(listed, static r => r.Event.DeliveryKey == "github:1");
    }

    [Fact]
    public void An_older_event_seen_late_into_a_full_store_is_the_one_dropped()
    {
        var recent = new RecentEvents();
        for (var i = 1; i <= RecentEvents.PerKind; i++)
        {
            recent.Record(_work, Event("github:" + i.ToString(CultureInfo.InvariantCulture), minutes: i + 10));
        }

        recent.Record(_work, Event("github:late", minutes: 0));

        Assert.DoesNotContain(recent.Newest(_merged, _work, max: 50), static r => r.Event.DeliveryKey == "github:late");
    }

    [Fact]
    public void Kinds_and_connections_are_kept_apart_and_no_connection_lists_every_one()
    {
        var recent = new RecentEvents();
        recent.Record(_work, Event("github:1", minutes: 1));
        recent.Record(_home, Event("github:2", minutes: 2));
        recent.Record(_work, Event("github:3", minutes: 3, EventKind.GitHubPrOpened));

        Assert.Equal(["github:1"], recent.Newest(_merged, _work, max: 10).Select(static r => r.Event.DeliveryKey), StringComparer.Ordinal);
        Assert.Equal(["github:2", "github:1"], recent.Newest(_merged, connection: null, max: 10).Select(static r => r.Event.DeliveryKey), StringComparer.Ordinal);
        Assert.Equal(["github:3"], recent.Newest(EventKind.GitHubPrOpened, connection: null, max: 10).Select(static r => r.Event.DeliveryKey), StringComparer.Ordinal);
    }

    [Fact]
    public void Max_cuts_the_list()
    {
        var recent = new RecentEvents();
        recent.Record(_work, Event("github:1", minutes: 1));
        recent.Record(_work, Event("github:2", minutes: 2));

        Assert.Equal(["github:2"], recent.Newest(_merged, _work, max: 1).Select(static r => r.Event.DeliveryKey), StringComparer.Ordinal);
    }
}
