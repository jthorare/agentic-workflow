namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>GET /sindri/connections/{name}.</summary>
/// <param name="Connection">The connection.</param>
/// <param name="LastSignInFailure">How the last sign-in of a connected row failed, or null.</param>
internal sealed record ConnectionOne(ConnectionView Connection, ConnectionSignInFailure? LastSignInFailure = null);
