using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>
/// The events the poll saw on this box, for the trigger step's "Recent events" and a test run's input (#3201, spec §4.3, §9 E6): the last
/// <see cref="PerKind"/> per (connection, kind), newest first by the provider's time, one row per delivery id. In memory: a restart starts it empty.
/// </summary>
internal sealed class RecentEvents
{
    /// <summary>How many events one (connection, kind) keeps.</summary>
    internal const int PerKind = 20;

    /// <summary>Keeps one event; a delivery id already kept is replaced, and past <see cref="PerKind"/> the oldest is dropped.</summary>
    /// <param name="connection">The connection it came from.</param>
    /// <param name="polled">The event.</param>
    internal void Record(ConnectionName connection, PolledEvent polled) => throw new NotSupportedException("red: #3201");

    /// <summary>The newest events of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="connection">One connection, or null for every one.</param>
    /// <param name="max">The most to return.</param>
    /// <returns>Newest first.</returns>
    internal IReadOnlyList<RecentEvent> Newest(EventKind kind, ConnectionName? connection, int max) => throw new NotSupportedException("red: #3201");
}

/// <summary>One kept event and the connection it came from.</summary>
/// <param name="Connection">The connection.</param>
/// <param name="Event">The event.</param>
internal sealed record RecentEvent(ConnectionName Connection, PolledEvent Event);
