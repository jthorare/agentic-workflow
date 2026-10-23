using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>
/// The engine part of the needs-you fold (API P11): a root instance's 'workflow.*' rows put or clear its 'park' row. The items projection
/// folds the same event first, so the item row's engine part is the instance as the event left it; this reads it (and the item's title and
/// repo) and never the other way round. A child instance's row only moves the parked descendants its root's 'park' row describes (<see cref="NestedPark"/>); each root of the item (the latest, or an earlier one still live) settles its own rows, and a root the item no longer holds has ended, so its rows clear.
/// </summary>
internal sealed partial class NeedsYouProjection
{
    private const string ParkPrefix = "park:";

    private async ValueTask<Result<Unit>> Engine(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var payload = events.Payload<WorkflowEventPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        if (recorded.Item is not { } key)
        {
            return Unit.Value;
        }

        if (!EngineItemStates.IsRoot(payload.Value.Instance))
        {
            return await Nested(recorded, rows, key, payload.Value, cancellationToken).ConfigureAwait(false);   // a child instance's row reaches the list through its root's park row
        }

        var item = await rows.Get<ItemRow>(ViewNames.Items, key, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        // no item row: skipped, not a stall. Otherwise the row is settled by the root's own part (the latest root's, or an earlier live one's); a root the item no longer holds ended, so its rows clear
        if (item.Value is { } ended && EngineClosure.IsEnded(ended))
        {
            var voided = await ClearDecisions(rows, key, cancellationToken).ConfigureAwait(false);   // the items projection already folded this row: the item ended, so what it waited on is void (as a human close does)
            if (voided.IsError)
            {
                return voided;
            }
        }

        return item.Value is { } row
            ? await Settle(rows, (row, EngineItemStates.PartOf(row.Engine, payload.Value.Instance)), payload.Value, recorded, cancellationToken).ConfigureAwait(false)
            : Unit.Value;
    }

    private async ValueTask<Result<Unit>> Settle(IViewRows rows, (ItemRow Item, EngineItemPart? Part) at, WorkflowEventPayload payload, LedgerEvent recorded, CancellationToken cancellationToken)
    {
        var instance = WorkflowInstanceId.Of(InstanceAddress.From(payload.Instance)).Value;
        var existing = await rows.Get<NeedsYouRowRecord>(ViewNames.NeedsYou, ParkPrefix + instance, cancellationToken).ConfigureAwait(false);
        if (existing.IsError)
        {
            return existing.Error;
        }

        var plan = new SettlePlan(instance, at.Item, at.Part,
            at.Part is { } part ? Wanted(at.Item, part, payload) : null,
            at.Part is { Ended: false } live ? Kept(live, payload) : null,   // a takeover row stays until the handback or the end; a park row stays across a row that doesn't say
            at.Part is { Ended: false } ? existing.Value?.Nested : null);   // the parked descendants outlive the root's own park, not the root
        foreach (var prefix in new[] { ParkPrefix, "desk:", "takeover:" })
        {
            var outcome = await SettlePrefix(rows, plan, prefix, recorded, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome;
            }
        }

        return Unit.Value;
    }

    /// <summary>What a root's event settles: the instance, the item and part, the row it wants, the prefix that stays and the parked descendants its park row carries.</summary>
    private sealed record SettlePlan(string Instance, ItemRow Item, EngineItemPart? Part, (string Prefix, Func<DateTimeOffset, NeedsYouRow> Build)? Wanted, string? Keeps, IReadOnlyList<NestedPark>? Nested);

    /// <summary>One row to put: its id, how to build it and the parked descendants its record carries.</summary>
    private sealed record RowPut(string Id, Func<DateTimeOffset, NeedsYouRow> Build, IReadOnlyList<NestedPark>? Nested);

    /// <summary>One prefix of the root: its wanted row is put, a kept row stays, a park prefix with parked descendants shows the deepest one, any other row is deleted.</summary>
    private async ValueTask<Result<Unit>> SettlePrefix(IViewRows rows, SettlePlan plan, string prefix, LedgerEvent recorded, CancellationToken cancellationToken)
    {
        var park = string.Equals(prefix, ParkPrefix, StringComparison.Ordinal);
        if (plan.Wanted is { } wanted && string.Equals(wanted.Prefix, prefix, StringComparison.Ordinal))
        {
            return await PutEngine(rows, new RowPut(prefix + plan.Instance, wanted.Build, park ? plan.Nested : null), recorded, cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(plan.Keeps, prefix, StringComparison.Ordinal))
        {
            return Unit.Value;
        }

        return park && plan.Nested is { Count: > 0 } parked && plan.Part is { } part
            ? await PutEngine(rows, NestedPut(plan.Item, InstanceAddress.From(part.Address), parked), recorded, cancellationToken).ConfigureAwait(false)   // the root doesn't park: the deepest parked descendant does
            : await rows.Delete(ViewNames.NeedsYou, prefix + plan.Instance, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The row prefix that stays as it is: a takeover's while the instance is taken over, a park's or escalation's when this row carries no detail to say what changed.</summary>
    private static string? Kept(EngineItemPart part, WorkflowEventPayload payload) => part.Suspension switch
    {
        SuspensionKind.Takeover => "takeover:",
        SuspensionKind.Park or SuspensionKind.Escalation when payload.Detail is null => "park:",
        _ => null,
    };

    private (string Prefix, Func<DateTimeOffset, NeedsYouRow> Build)? Wanted(ItemRow item, EngineItemPart part, WorkflowEventPayload payload)
    {
        var address = InstanceAddress.From(part.Address);
        if (part.Ended)
        {
            return null;
        }

        return part.Suspension switch
        {
            SuspensionKind.Park or SuspensionKind.Escalation when payload.Detail?.Delta.Suspension is { } suspension =>
                (ParkPrefix, since => NeedsYouRows.ForPark(item, new ParkRowInput(address, suspension, payload.Detail?.Reason), since, scrubber)),
            SuspensionKind.Takeover when string.Equals(payload.Trigger, TriggerNames.TakeoverIdle, StringComparison.Ordinal) =>
                ("takeover:", since => NeedsYouRows.ForIdleTakeover(item, address, since, scrubber)),
            null when part is { RunOpen: true, Runtime: StepRuntime.Desk } => ("desk:", since => NeedsYouRows.ForDeskTask(item, address, since, scrubber)),
            _ => null,
        };
    }

    private static async ValueTask<Result<Unit>> PutEngine(IViewRows rows, RowPut put, LedgerEvent recorded, CancellationToken cancellationToken)
    {
        var existing = await rows.Get<NeedsYouRowRecord>(ViewNames.NeedsYou, put.Id, cancellationToken).ConfigureAwait(false);
        if (existing.IsError)
        {
            return existing.Error;
        }

        var row = put.Build(existing.Value?.Row.Since ?? recorded.Envelope.OccurredAt);   // the row keeps the time it first appeared
        return await rows.Put(ViewNames.NeedsYou, put.Id, new NeedsYouRowRecord(row, recorded.Sequence, put.Nested), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A descendant's row (API spec section 3.6): its park, if its delta shows one, joins the parked descendants of its root and its leaving the park
    /// (a resume, an answer, a cancel, its end) leaves them. The root's 'park' row then describes the deepest parked descendant (the longest address, the
    /// later park on a tie), unless the root parks itself, when its own row wins and the descendants wait on the record. A row stored without detail says nothing.
    /// </summary>
    private async ValueTask<Result<Unit>> Nested(LedgerEvent recorded, IViewRows rows, string key, WorkflowEventPayload payload, CancellationToken cancellationToken)
    {
        if (payload.Detail is null)
        {
            return Unit.Value;
        }

        var root = string.Join('/', payload.Instance.Split('/').Take(3));
        var item = await rows.Get<ItemRow>(ViewNames.Items, key, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        return item.Value is { } row && EngineItemStates.PartOf(row.Engine, root) is { Ended: false } part   // no live root holding this descendant: nothing to do
            ? await Descended(rows, new DescendantRow(row, part, InstanceAddress.From(root), payload, recorded), cancellationToken).ConfigureAwait(false)
            : Unit.Value;
    }

    /// <summary>A descendant's row with the live root that holds it.</summary>
    private sealed record DescendantRow(ItemRow Item, EngineItemPart Part, InstanceAddress Root, WorkflowEventPayload Payload, LedgerEvent Recorded);

    private async ValueTask<Result<Unit>> Descended(IViewRows rows, DescendantRow descendant, CancellationToken cancellationToken)
    {
        var id = ParkPrefix + WorkflowInstanceId.Of(descendant.Root).Value;
        var existing = await rows.Get<NeedsYouRowRecord>(ViewNames.NeedsYou, id, cancellationToken).ConfigureAwait(false);
        if (existing.IsError)
        {
            return existing.Error;
        }

        var parked = ParkedDescendants(existing.Value?.Nested, descendant);
        if (descendant.Part.Suspension is SuspensionKind.Park or SuspensionKind.Escalation)
        {
            return existing.Value is { } own
                ? await rows.Put(ViewNames.NeedsYou, id, own with { Nested = parked.Count == 0 ? null : parked }, cancellationToken).ConfigureAwait(false)   // the root's own row wins; the descendants wait on its record
                : Unit.Value;
        }

        return parked.Count == 0
            ? await rows.Delete(ViewNames.NeedsYou, id, cancellationToken).ConfigureAwait(false)
            : await PutEngine(rows, NestedPut(descendant.Item, descendant.Root, parked), descendant.Recorded, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The parked descendants after this row: the descendant's earlier entry leaves, and it joins again when its delta shows a park or escalation.</summary>
    private static List<NestedPark> ParkedDescendants(IReadOnlyList<NestedPark>? before, DescendantRow descendant)
    {
        var instance = descendant.Payload.Instance;
        List<NestedPark> parked = [.. (before ?? []).Where(p => !string.Equals(p.Address, instance, StringComparison.Ordinal))];
        if (descendant.Payload.Detail?.Delta.Suspension is { Kind: SuspensionKind.Park or SuspensionKind.Escalation } suspension)
        {
            parked.Add(new NestedPark(instance, suspension, descendant.Payload.Detail.Reason, descendant.Recorded.Sequence));
        }

        return parked;
    }

    private RowPut NestedPut(ItemRow item, InstanceAddress root, IReadOnlyList<NestedPark> parked) =>
        new(ParkPrefix + WorkflowInstanceId.Of(root).Value, since => NeedsYouRows.ForPark(item, Deepest(root, parked), since, scrubber), parked);

    /// <summary>The input of the row that describes the deepest of the parked descendants: the longest address, the later park on a tie.</summary>
    private static ParkRowInput Deepest(InstanceAddress root, IReadOnlyList<NestedPark> parked)
    {
        var deepest = parked.OrderByDescending(static p => p.Address.Count(static c => c == '/')).ThenByDescending(static p => p.Sequence).First();
        return new ParkRowInput(root, deepest.Suspension, deepest.Refusal, InstanceAddress.From(deepest.Address));
    }
}
