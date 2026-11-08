using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The bridge's 'route: item' targets and its held window (engine spec §5.4). An item event goes to the item's live
/// instances as the visibility query lists them, but visibility lags a start: an instance started just before its event
/// can be missing from the list, and the cursor has moved on by the time it shows up. So every item event stays in the
/// window for <see cref="VisibilityLag"/> after it was delivered, and each bridge run re-sends it to a live instance of
/// its item that hasn't had it and that started at or before the event happened. An instance that started after it (a
/// later attempt) never gets it from the window: the event is not about that attempt. The workflow drops a repeat by its
/// ledger sequence. The window is in memory: it covers the seconds visibility lags, not a serve restart.
/// </summary>
/// <param name="instances">The live instances, by a visibility query on 'Item'.</param>
/// <param name="clock">The bridge's clock.</param>
internal sealed class HeldItemEvents(IWorkflowInstances instances, TimeProvider clock)
{
    /// <summary>How long after its delivery an item event stays in the window: the time visibility may lag a start.</summary>
    internal static readonly TimeSpan VisibilityLag = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private readonly List<Held> _held = [];

    /// <summary>The live instances of <paramref name="item"/>.</summary>
    /// <param name="item">The item (an <see cref="ItemKey"/>'s text).</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Their addresses.</returns>
    public async ValueTask<Result<IReadOnlyList<InstanceAddress>>> LiveOf(string item, CancellationToken cancellationToken)
    {
        var live = await Started(item, cancellationToken).ConfigureAwait(false);
        return live.IsError ? live.Error : live.Value.Select(static i => i.Address).ToList();
    }

    /// <summary>Keeps <paramref name="trigger"/> in the window, with the instances that already have it.</summary>
    /// <param name="item">The event's item.</param>
    /// <param name="trigger">The event as a wait's trigger (its time is <see cref="WaitSatisfied.OccurredAt"/>).</param>
    /// <param name="signalled">The instances it was sent to.</param>
    public void Hold(string item, WaitSatisfied trigger, IEnumerable<InstanceAddress> signalled)
    {
        lock (_gate)
        {
            _held.Add(new Held(item, trigger, clock.GetUtcNow(), [.. signalled]));
        }
    }

    /// <summary>
    /// Drops what is past the visibility lag, then lists the live instances of each held item (one query per item) that
    /// haven't had an event and started at or before it.
    /// </summary>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The deliveries still owed; mark each with <see cref="Delivered"/> once it is sent.</returns>
    public async ValueTask<Result<IReadOnlyList<(WaitSatisfied Trigger, InstanceAddress Target)>>> Owed(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        Held[] held;
        lock (_gate)
        {
            _ = _held.RemoveAll(h => h.Trigger.OccurredAt is null || now - h.DeliveredAt >= VisibilityLag);
            held = [.. _held];
        }

        List<(WaitSatisfied, InstanceAddress)> owed = [];
        foreach (var byItem in held.GroupBy(static h => h.Item, StringComparer.Ordinal))
        {
            var live = await Started(byItem.Key, cancellationToken).ConfigureAwait(false);
            if (live.IsError)
            {
                return live.Error;
            }

            foreach (var one in byItem)
            {
                var happened = one.Trigger.OccurredAt.GetValueOrDefault(); // why: a held event without a time was dropped above
                owed.AddRange(live.Value
                    .Where(i => i.StartedAt is { } started && started <= happened && !one.Signalled.Contains(i.Address))
                    .Select(i => (one.Trigger, i.Address)));
            }
        }

        return owed;
    }

    /// <summary>Records that <paramref name="target"/> has <paramref name="trigger"/> now.</summary>
    /// <param name="trigger">The held event.</param>
    /// <param name="target">The instance it was sent to.</param>
    public void Delivered(WaitSatisfied trigger, InstanceAddress target)
    {
        lock (_gate)
        {
            foreach (var one in _held.Where(h => ReferenceEquals(h.Trigger, trigger)))
            {
                _ = one.Signalled.Add(target);
            }
        }
    }

    private async ValueTask<Result<List<Live>>> Started(string item, CancellationToken cancellationToken)
    {
        var live = await instances.List($"Item = '{item.Replace("'", "''", StringComparison.Ordinal)}'", ended: false, cancellationToken).ConfigureAwait(false);
        return live.IsError
            ? live.Error
            : live.Value.Select(static row => InstanceAddress.TryFrom(row.Address, out var address) ? new Live(address, row.StartedAt) : null).OfType<Live>().ToList();
    }

    private sealed record Live(InstanceAddress Address, DateTimeOffset? StartedAt);

    private sealed record Held(string Item, WaitSatisfied Trigger, DateTimeOffset DeliveredAt, HashSet<InstanceAddress> Signalled);
}
