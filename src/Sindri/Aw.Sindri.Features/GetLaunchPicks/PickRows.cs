using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>What one scan of the pick, usage and launch-failed rows found: one entry per run with a 'model.picking' or a 'model.picked', the last usage sample of each launch and the launches that failed to start.</summary>
/// <param name="events">Reads each event's payload.</param>
/// <param name="item">Keeps only this item's picks, or null for every item.</param>
/// <param name="instance">Keeps only this instance's picks, or null for every instance.</param>
internal sealed class PickRows(EventFactory events, ItemId? item, InstanceAddress? instance)
{
    private readonly Dictionary<LaunchId, PickEntry> _entries = [];

    /// <summary>The last 'session.usage' sample per launch.</summary>
    public Dictionary<LaunchId, SessionUsage> Usage { get; } = [];

    /// <summary>The launches with a 'launch.failed'.</summary>
    public HashSet<LaunchId> LaunchFailed { get; } = [];

    /// <summary>The runs, oldest first.</summary>
    public IReadOnlyList<PickEntry> Picks => [.. _entries.Values.OrderBy(static e => e.Sequence)];

    /// <summary>Takes a scanned row in (rows of other types are ignored).</summary>
    /// <param name="row">The row.</param>
    public void Add(LedgerEvent row)
    {
        switch (row.Envelope.Type)
        {
            case EventTypes.ModelPicking:
                AddPicking(row);
                break;
            case EventTypes.ModelPicked:
                AddPicked(row);
                break;
            case EventTypes.SessionUsage:
                AddUsage(row);
                break;
            case EventTypes.LaunchFailed:
                AddFailed(row);
                break;
            default:
                break;
        }
    }

    private bool Keeps(ItemId rowItem, InstanceAddress rowInstance) =>
        (item is not { } wanted || rowItem == wanted) && (instance is not { } address || rowInstance == address);

    private void AddPicking(LedgerEvent row)
    {
        var read = events.Payload<ModelPicking>(row);
        if (!read.IsError && Keeps(read.Value.Item, read.Value.Instance))
        {
            var picking = read.Value;
            _entries[picking.Launch] = new PickEntry(picking.Launch, row.Sequence, row.Envelope.OccurredAt, picking.Item, picking.Instance, picking.State, picking.Attempt, Picked: null);
        }
    }

    private void AddPicked(LedgerEvent row)
    {
        var read = events.Payload<ModelPicked>(row);
        if (read.IsError || !Keeps(read.Value.Item, read.Value.Instance))
        {
            return;
        }

        var picked = read.Value;
        _entries[picked.Launch] = _entries.TryGetValue(picked.Launch, out var earlier)
            ? earlier with { Picked = picked }
            : new PickEntry(picked.Launch, row.Sequence, row.Envelope.OccurredAt, picked.Item, picked.Instance, picked.State, picked.Attempt, picked);
    }

    private void AddUsage(LedgerEvent row)
    {
        var read = events.Payload<SessionUsage>(row);
        if (!read.IsError && (!Usage.TryGetValue(read.Value.Launch, out var last) || read.Value.Sample >= last.Sample))
        {
            Usage[read.Value.Launch] = read.Value;
        }
    }

    private void AddFailed(LedgerEvent row)
    {
        var read = events.Payload<LaunchFailed>(row);
        if (!read.IsError)
        {
            _ = LaunchFailed.Add(read.Value.Launch);
        }
    }
}
