using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Tests.Connections;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PollConnections;

/// <summary>The whole path with the real Linear poller over the fake Linear: baseline, a label added, the service's next due poll, one run with the event in its trigger.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowPollGoldenTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(PollSettings.DefaultIntervalSeconds);

    [Fact]
    public async Task A_Linear_label_change_fires_the_workflow_within_one_interval()
    {
        var world = new PollWorld();
        var provider = new FakeProvider();
        using var harness = PollerHarness.For("linear", provider);
        _ = await world.Connections.Store.Upsert(harness.Connection, PollWorld.Token);
        await world.Connections.Hold();
        var slug = await world.GoLive("bug-triage", filter: "trigger.label == \"bug\"");

        _ = await world.Tick(harness.Poller);   // the start's catch-up: a baseline, so nothing old starts a run
        Assert.Empty(world.Starter.Requests);

        var at = world.Connections.Clock.GetUtcNow().AddMinutes(1).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        provider.AddLinearIssue(ProviderFixtures.LinearLabelAdded("ENG-7", "Crash on save", "h-55", "bug", "lbl-1", at));
        provider.AddLinearIssue(ProviderFixtures.LinearLabelAdded("ENG-8", "Typo", "h-56", "docs", "lbl-2", at));
        var asked = provider.Requests.Count;

        world.Connections.Clock.Advance(_interval / 2);
        _ = await world.Tick(harness.Poller);
        Assert.Equal(asked, provider.Requests.Count);   // not due yet: the provider is not asked
        Assert.Empty(world.Starter.Requests);

        world.Connections.Clock.Advance(_interval);
        _ = await world.Tick(harness.Poller);

        var start = Assert.Single(world.Starter.Requests);   // the 'docs' label is filtered out by the trigger
        Assert.Equal(slug, start.Slug);
        var trigger = JsonNode.Parse(start.Trigger)!;
        Assert.Equal("connection_event", trigger["kind"]!.GetValue<string>());
        Assert.Equal("linear:h-55:label:lbl-1", trigger["deliveryId"]!.GetValue<string>());
        Assert.Equal("ENG-7", trigger["identifier"]!.GetValue<string>());
        Assert.Contains(EventTypes.ConnectionPolled, world.Recorded());
    }
}
