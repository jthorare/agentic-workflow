using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>Every active trigger that watches one (connection, event kind): the provider is called once for the group and each trigger filters the events itself.</summary>
/// <param name="Connection">The connection.</param>
/// <param name="Kind">The event kind.</param>
/// <param name="Triggers">The triggers.</param>
internal sealed record PollGroup(ConnectionName Connection, EventKind Kind, IReadOnlyList<PollTrigger> Triggers);
