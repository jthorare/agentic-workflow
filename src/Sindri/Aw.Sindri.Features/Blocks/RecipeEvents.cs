using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks;

/// <summary>The events the first recipes wait for (spec §12).</summary>
internal static class RecipeEvents
{
    private const string Dispatcher = "system:dispatcher";

    /// <summary>'item.routed', from the router stub: whether the item is startable, with its Step spec.</summary>
    public static EventDescriptor ItemRouted { get; } = new(
        EventKind.From("item.routed"), "router.stub", EventRoute.Item,
        [OutcomeName.From("startable"), OutcomeName.From("notStartable")], [FactKey.From("fact:stepSpec")]);

    /// <summary>'claim.created', from the dispatcher, answering the 'effect.claim.ref' command that caused it.</summary>
    public static EventDescriptor ClaimCreated { get; } = new(
        EventKind.From("claim.created"), Dispatcher, EventRoute.Causation,
        [OutcomeName.From("created"), OutcomeName.From("claimExists")], []);

    /// <summary>'notify.delivered', from the dispatcher, answering the 'effect.notify.desk' command that caused it.</summary>
    public static EventDescriptor NotifyDelivered { get; } = new(
        EventKind.From("notify.delivered"), Dispatcher, EventRoute.Causation, [OutcomeName.From("delivered")], []);
}
