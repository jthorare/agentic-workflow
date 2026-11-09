using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>What <see cref="CatchUp"/> reads: one group through one poller, from a saved cursor.</summary>
/// <param name="Group">The group (connection, event kind and its triggers).</param>
/// <param name="Connection">The connection record.</param>
/// <param name="Poller">The poller for the connection's preset.</param>
/// <param name="Cursor">The saved cursor, or null when the group was never polled.</param>
/// <param name="Pages">How many provider calls a forward read makes at most.</param>
/// <param name="Forward">True for a catch-up (repeat the call), false for one regular poll.</param>
internal sealed record ReadRequest(PollGroup Group, Connection Connection, IEventPoller Poller, PollCursor? Cursor, int Pages, bool Forward);
