using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The 'pr.*' events the PR babysitter and docs sync wait for (PB spec §6.1, W25): producer, route, outcomes and facts.</summary>
public sealed class PrEventsTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-pr-events-test");

    public static TheoryData<string, string, EventRoute, string, string> Events => new()
    {
        { "pr.changed", "service:pr-watch", EventRoute.Item, "merged closed updated review ci idle", "fact:prEvent" },
        { "pr.pushed", "system:dispatcher", EventRoute.Causation, "pushed pushedSource nothing headMoved rejected", "fact:push" },
        { "pr.restacked", "system:dispatcher", EventRoute.Causation, "restacked nothing conflict refused", "fact:restack" },
        { "pr.mergeResult", "system:dispatcher", EventRoute.Causation, "merged blocked headMoved", "fact:merge" },
        { "pr.docsMerged", "system:dispatcher", EventRoute.Causation, "merged pending none", "fact:docsMerge" },
        { "pr.opened", "system:dispatcher", EventRoute.Causation, "opened updated refused", "fact:pr" },
        { "pr.stacked", "system:dispatcher", EventRoute.Causation, "opened updated rejected", "fact:docsPr" },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Events))]
    public void The_event_is_registered_as_the_spec_describes(string kind, string producer, EventRoute route, string outcomes, string fact)
    {
        using var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider();

        var descriptor = provider.GetRequiredService<BlockCatalog>().FindEvent(EventKind.From(kind));

        Assert.NotNull(descriptor);
        Assert.Equal(producer, descriptor.Producer);
        Assert.Equal(route, descriptor.Route);
        Assert.Equal(outcomes.Split(' '), descriptor.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        Assert.Equal([fact], descriptor.Facts.Select(f => f.Value), StringComparer.Ordinal);
    }
}
