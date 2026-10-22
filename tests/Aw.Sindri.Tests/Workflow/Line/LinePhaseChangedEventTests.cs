using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>The event a line waits on to start its next phases (line spec LN-WF1, W25): producer, route, outcomes and fact.</summary>
public sealed class LinePhaseChangedEventTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-line-event-test");

    [Fact, Trait("Lane", "unit")]
    public void The_event_is_registered_with_its_six_outcomes_routed_to_the_linked_parent()
    {
        using var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider();

        var descriptor = provider.GetRequiredService<BlockCatalog>().FindEvent(EventKind.From("line.phaseChanged"));

        Assert.NotNull(descriptor);
        Assert.Equal("system:dispatcher", descriptor.Producer);
        Assert.Equal(EventRoute.Parent, descriptor.Route);
        Assert.Equal(["started", "shipped", "parked", "resumed", "merged", "ended", "failed"], descriptor.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        Assert.Equal(["fact:phaseEvent"], descriptor.Facts.Select(f => f.Value), StringComparer.Ordinal);
    }
}
