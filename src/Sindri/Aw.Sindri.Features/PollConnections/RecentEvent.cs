using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>One kept event and the connection it came from.</summary>
/// <param name="Connection">The connection.</param>
/// <param name="Event">The event.</param>
internal sealed record RecentEvent(ConnectionName Connection, PolledEvent Event);
