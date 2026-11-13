using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>
/// The events the poll saw on this box, for the trigger step's "Recent events" and a test run's input (#3201, spec §4.3, §9 E6): the last
/// <see cref="PerKind"/> per (connection, kind), newest first by the provider's time, one row per delivery id. In memory: a restart starts it empty.
/// </summary>
internal sealed class RecentEvents
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(ConnectionName Connection, string Kind), List<PolledEvent>> _byGroup = [];

    /// <summary>How many events one (connection, kind) keeps.</summary>
    internal const int PerKind = 20;

    /// <summary>Keeps one event; a delivery id already kept is replaced, and past <see cref="PerKind"/> the oldest is dropped.</summary>
    /// <param name="connection">The connection it came from.</param>
    /// <param name="polled">The event.</param>
    internal void Record(ConnectionName connection, PolledEvent polled)
    {
        ArgumentNullException.ThrowIfNull(polled);
        lock (_gate)
        {
            var key = (connection, polled.Kind.Key);
            if (!_byGroup.TryGetValue(key, out var kept))
            {
                kept = [];
                _byGroup[key] = kept;
            }

            _ = kept.RemoveAll(e => string.Equals(e.DeliveryKey, polled.DeliveryKey, StringComparison.Ordinal));
            kept.Add(polled);
            // newest first; the oldest is last, so the cut drops it. why: List.Sort is unstable, so rows with one time order by delivery id and the cut is the same every time
            kept.Sort(static (a, b) => b.OccurredAt != a.OccurredAt ? b.OccurredAt.CompareTo(a.OccurredAt) : string.CompareOrdinal(a.DeliveryKey, b.DeliveryKey));
            if (kept.Count > PerKind)
            {
                kept.RemoveRange(PerKind, kept.Count - PerKind);
            }
        }
    }

    /// <summary>The newest events of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="connection">One connection, or null for every one.</param>
    /// <param name="max">The most to return.</param>
    /// <returns>Newest first.</returns>
    internal IReadOnlyList<RecentEvent> Newest(EventKind kind, ConnectionName? connection, int max)
    {
        lock (_gate)
        {
            return [.. _byGroup
                .Where(g => string.Equals(g.Key.Kind, kind.Key, StringComparison.Ordinal) && (connection is null || g.Key.Connection == connection.Value))
                .SelectMany(static g => g.Value.Select(e => new RecentEvent(g.Key.Connection, e)))
                .OrderByDescending(static r => r.Event.OccurredAt)
                .ThenBy(static r => r.Event.DeliveryKey, StringComparer.Ordinal)
                .Take(Math.Max(max, 0))];
        }
    }
}
