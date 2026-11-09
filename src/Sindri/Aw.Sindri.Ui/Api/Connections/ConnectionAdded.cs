namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>POST /sindri/connections and POST /sindri/connections/{name}/signin: the connection and, for a browser sign-in, where to go.</summary>
/// <param name="Connection">The connection.</param>
/// <param name="ConsentUrl">The consent page to open, or null for a key connection.</param>
internal sealed record ConnectionAdded(ConnectionView Connection, string? ConsentUrl);
