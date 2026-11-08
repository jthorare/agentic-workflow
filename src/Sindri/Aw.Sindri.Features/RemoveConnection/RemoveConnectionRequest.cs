namespace Aw.Sindri.Features.RemoveConnection;

/// <summary>Remove a connection and its secret ('sindri connection remove &lt;name&gt;'; DELETE /sindri/connections/{name}; MCP connection_remove).</summary>
/// <param name="Name">The connection name.</param>
public sealed record RemoveConnectionRequest(string Name);
