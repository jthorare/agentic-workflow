using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>The added connection.</summary>
/// <param name="Connection">The connection; an oauth one still signing in has status 'needs-sign-in'.</param>
/// <param name="ConsentUrl">The provider's consent page while a sign-in is open (an agent hands it to the owner), else null.</param>
public sealed record AddConnectionResponse(ConnectionView Connection, string? ConsentUrl);
