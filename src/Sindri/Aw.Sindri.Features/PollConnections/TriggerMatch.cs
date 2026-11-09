using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>Decides whether a trigger wants an event, and builds the trigger JSON of the run it starts.</summary>
internal static class TriggerMatch
{
    internal const string Kind = "connection_event";

    /// <summary>Applies a trigger's filter to an event.</summary>
    /// <param name="filter">The filter text, or null.</param>
    /// <param name="connection">The connection.</param>
    /// <param name="polled">The event.</param>
    /// <returns>True when the trigger takes the event.</returns>
    internal static bool Wants(string? filter, ConnectionName connection, PolledEvent polled) => throw new NotSupportedException("unbuilt: " + filter + connection + polled);

    /// <summary>The trigger JSON of a run.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="polled">The event.</param>
    /// <returns>The JSON text.</returns>
    internal static string Trigger(ConnectionName connection, PolledEvent polled) => throw new NotSupportedException("unbuilt: " + connection + polled);
}
