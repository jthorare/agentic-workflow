using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Writes the model pick's events under the launch's keys and reads a stored pick back (model-pick spec 5): 'model.picking:&lt;launch&gt;' and 'model.pick:&lt;launch&gt;', first write wins, the pattern
/// <see cref="AgentLaunchRecorder"/> uses for 'launch.outcome:'.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds; every append is fenced by it.</param>
/// <param name="events">Builds the entries.</param>
internal sealed class ModelPickStore(ILedgerStore ledger, HeldLease lease, EventFactory events)
{
    private const int PageSize = 200;
    private const int MaxPages = 25;

    /// <summary>Records that a pick began.</summary>
    /// <param name="picking">The payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The position, with AlreadyRecorded when an earlier attempt began it.</returns>
    public Task<LedgerAppend> Picking(ModelPicking picking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(picking);
        return Append(events.Entry(EventTypes.ModelPicking, picking, "model.picking:" + picking.Launch.Value, picking.Item.Value), cancellationToken);
    }

    /// <summary>Records the pick.</summary>
    /// <param name="picked">The payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The position, with AlreadyRecorded when the launch was picked before.</returns>
    public Task<LedgerAppend> Picked(ModelPicked picked, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(picked);
        return Append(events.Entry(EventTypes.ModelPicked, picked, "model.pick:" + picked.Launch.Value, picked.Item.Value), cancellationToken);
    }

    /// <summary>The stored 'model.picked' of <paramref name="launch"/> after sequence <paramref name="after"/>, or null.</summary>
    /// <param name="after">The cursor.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pick, if any.</returns>
    public async Task<ModelPicked?> Find(long after, LaunchId launch, CancellationToken cancellationToken)
    {
        var key = "model.pick:" + launch.Value;
        var cursor = after;
        for (var page = 0; page < MaxPages; page++)
        {
            var read = await ledger.ReadAfter(cursor, PageSize, cancellationToken).ConfigureAwait(false);
            if (read.IsError || read.Value.Count == 0)
            {
                return null;
            }

            foreach (var row in read.Value)
            {
                if (string.Equals(row.IdempotencyKey, key, StringComparison.Ordinal) && string.Equals(row.Envelope.Type, EventTypes.ModelPicked, StringComparison.Ordinal))
                {
                    var payload = events.Payload<ModelPicked>(row);
                    return payload.IsError ? null : payload.Value;
                }
            }

            cursor = read.Value[^1].Sequence;
            if (read.Value.Count < PageSize)
            {
                return null;
            }
        }

        return null;
    }

    private async Task<LedgerAppend> Append(LedgerEntry entry, CancellationToken cancellationToken)
    {
        var grant = lease.Current ?? throw new InvalidOperationException("SND-LEASE-002: this process holds no lease; the model pick waits for one");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? throw new InvalidOperationException($"{appended.Error.Code}: {appended.Error.Message}") : appended.Value;
    }
}
