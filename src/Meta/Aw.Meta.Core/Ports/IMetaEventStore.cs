using Aw.Kernel;
using Aw.Meta.Core.Events;

namespace Aw.Meta.Core.Ports;

/// <summary>aw-meta's append-only event log, read by a cursor like "sindri ledger --after N".</summary>
public interface IMetaEventStore
{
    /// <summary>Appends an event and drops the events older than <see cref="MetaEvent.Retention"/> before its time, in one transaction. The kind must be dotted lower-case and the fields one JSON object (AW-META-027 otherwise).</summary>
    /// <param name="metaEvent">The event; its Id is ignored.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new event's id.</returns>
    public ValueTask<Result<long>> Append(MetaEvent metaEvent, CancellationToken cancellationToken);

    /// <summary>The events with an id above <paramref name="after"/>, oldest first, with the log's oldest and latest ids read in the same transaction.</summary>
    /// <param name="after">The cursor: the last id the reader has.</param>
    /// <param name="kind">Only this kind, or null for every kind.</param>
    /// <param name="lane">Only this lane, or null for every lane.</param>
    /// <param name="max">At most this many events.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page: the events ascending by id, the oldest id still kept and the highest id ever assigned.</returns>
    public ValueTask<Result<MetaEventPage>> Read(long after, string? kind, string? lane, int max, CancellationToken cancellationToken);

    /// <summary>Drops the events older than <paramref name="olderThan"/>.</summary>
    /// <param name="olderThan">The cutoff.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many events were dropped.</returns>
    public ValueTask<Result<int>> Prune(DateTimeOffset olderThan, CancellationToken cancellationToken);
}
