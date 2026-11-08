using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.EngineReads;

/// <summary>
/// Which ledger rows mirror an instance's Temporal events (port; API spec §3.8, E-30; engine spec §5.6): the row's 'temporal_event_id' is the activity id
/// of the RecordEvent that wrote it, and its idempotency key names the run. A read of the ledger, never a write.
/// </summary>
public interface IMirroredSequences
{
    /// <summary>The ledger sequence of each activity id that has a mirrored row (the newest row when a replay left two).</summary>
    /// <param name="address">The instance.</param>
    /// <param name="run">The run the events belong to.</param>
    /// <param name="activities">The activity ids to look up.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sequence of each id that has a row; an id with none is absent.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, long>>> Of(InstanceAddress address, string run, IReadOnlyCollection<string> activities, CancellationToken cancellationToken);
}
