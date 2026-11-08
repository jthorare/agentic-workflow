using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.ListConnections;

/// <summary>The connections, sorted by name.</summary>
/// <param name="Connections">The views.</param>
public sealed record ListConnectionsResponse(IReadOnlyList<ConnectionView> Connections);
