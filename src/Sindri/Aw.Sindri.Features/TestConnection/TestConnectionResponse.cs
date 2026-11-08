using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.TestConnection;

/// <summary>The result of a test. The connection's status and reason are updated to match.</summary>
/// <param name="Connection">The connection after the test.</param>
/// <param name="Ok">Whether the provider accepted the credential.</param>
/// <param name="Reason">Why not (no token in it), or null.</param>
public sealed record TestConnectionResponse(ConnectionView Connection, bool Ok, string? Reason);
