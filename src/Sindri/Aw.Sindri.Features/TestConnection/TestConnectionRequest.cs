namespace Aw.Sindri.Features.TestConnection;

/// <summary>Test a connection ('sindri connection test &lt;name&gt;'; POST /sindri/connections/{name}/test; MCP connection_test): read its secret and call the probe URL.</summary>
/// <param name="Name">The connection name.</param>
public sealed record TestConnectionRequest(string Name);
