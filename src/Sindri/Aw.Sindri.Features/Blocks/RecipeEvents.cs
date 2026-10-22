using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks;

/// <summary>The events the first recipes wait for (spec §12).</summary>
internal static class RecipeEvents
{
    private const string Dispatcher = "system:dispatcher";

    /// <summary>'item.routed', from the router stub: whether the item is startable, with its Step spec.</summary>
    public static EventDescriptor ItemRouted { get; } = new(
        EventKind.From("item.routed"), "service:router-stub", EventRoute.Item,
        [OutcomeName.From("startable"), OutcomeName.From("notStartable")], [FactKey.From("fact:stepSpec")]);

    /// <summary>'claim.created', from the dispatcher, answering the 'effect.claim.ref' command that caused it.</summary>
    public static EventDescriptor ClaimCreated { get; } = new(
        EventKind.From("claim.created"), Dispatcher, EventRoute.Causation,
        [OutcomeName.From("created"), OutcomeName.From("claimExists")], []);

    /// <summary>'notify.delivered', from the dispatcher, answering the 'effect.notify.desk' command that caused it.</summary>
    public static EventDescriptor NotifyDelivered { get; } = new(
        EventKind.From("notify.delivered"), Dispatcher, EventRoute.Causation, [OutcomeName.From("delivered")], []);

    /// <summary>
    /// 'line.phaseChanged' (line spec LN-WF1): a phase of a line changed state; the line waits on it to start the next ready phases. Its start reply, 'started', answers
    /// the line's own 'effect.line.start-phase'; the phase's 'effect.line.report' appends 'shipped', 'merged' and 'failed', and the engine the rest. Routed to the linked parent of the instance that caused it.
    /// </summary>
    public static EventDescriptor LinePhaseChanged { get; } = new(
        EventKind.From("line.phaseChanged"), Dispatcher, EventRoute.Parent, Outcomes("started", "shipped", "parked", "resumed", "merged", "ended", "failed"), [FactKey.From("fact:phaseEvent")]);

    /// <summary>'pr.changed', from the PR watcher (PB spec §6.1): the forge changed the PR; the outcome is the first change in this order, 'idle' after a quiet period.</summary>
    public static EventDescriptor PrChanged { get; } = new(
        EventKind.From("pr.changed"), "service:pr-watch", EventRoute.Item, Outcomes("merged", "closed", "updated", "review", "ci", "idle"), [FactKey.From("fact:prEvent")]);

    /// <summary>'pr.pushed', answering 'effect.pr.push'.</summary>
    public static EventDescriptor PrPushed { get; } = new(
        EventKind.From("pr.pushed"), Dispatcher, EventRoute.Causation, Outcomes("pushed", "pushedSource", "nothing", "headMoved", "rejected"), [FactKey.From("fact:push")]);

    /// <summary>'pr.restacked', answering 'effect.pr.restack'.</summary>
    public static EventDescriptor PrRestacked { get; } = new(
        EventKind.From("pr.restacked"), Dispatcher, EventRoute.Causation, Outcomes("restacked", "nothing", "conflict", "refused"), [FactKey.From("fact:restack")]);

    /// <summary>'pr.mergeResult', answering 'effect.pr.merge'.</summary>
    public static EventDescriptor PrMergeResult { get; } = new(
        EventKind.From("pr.mergeResult"), Dispatcher, EventRoute.Causation, Outcomes("merged", "blocked", "headMoved"), [FactKey.From("fact:merge")]);

    /// <summary>'pr.docsMerged', answering 'effect.pr.merge-docs'.</summary>
    public static EventDescriptor PrDocsMerged { get; } = new(
        EventKind.From("pr.docsMerged"), Dispatcher, EventRoute.Causation, Outcomes("merged", "pending", "none"), [FactKey.From("fact:docsMerge")]);

    /// <summary>'pr.stacked', answering 'effect.pr.create-stacked'.</summary>
    public static EventDescriptor PrStacked { get; } = new(
        EventKind.From("pr.stacked"), Dispatcher, EventRoute.Causation, Outcomes("opened", "updated", "rejected"), [FactKey.From("fact:docsPr")]);

    /// <summary>'pr.opened', answering 'effect.pr.open'.</summary>
    public static EventDescriptor PrOpened { get; } = new(
        EventKind.From("pr.opened"), Dispatcher, EventRoute.Causation, Outcomes("opened", "updated", "refused"), [FactKey.From("fact:pr")]);

    private static OutcomeName[] Outcomes(params string[] names) => [.. names.Select(OutcomeName.From)];
}
