using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.GetConnection;

/// <summary>One connection.</summary>
/// <param name="Connection">The view (never a token or a token prefix).</param>
public sealed record GetConnectionResponse(ConnectionView Connection);
