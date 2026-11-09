using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>What <see cref="PollRound"/> does once: one provider call for one group, from a saved cursor.</summary>
/// <param name="Group">The group.</param>
/// <param name="Connection">The connection record.</param>
/// <param name="Poller">The poller for the connection's preset.</param>
/// <param name="Cursor">The saved cursor, or null when the group was never polled.</param>
internal sealed record RoundRequest(PollGroup Group, Connection Connection, IEventPoller Poller, PollCursor? Cursor);
