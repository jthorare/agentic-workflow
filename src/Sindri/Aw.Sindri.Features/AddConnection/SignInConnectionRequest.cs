using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>Sign an existing oauth or mcp connection in again ('sindri connection signin &lt;name&gt;'; POST /sindri/connections/{name}/signin; MCP connection_signin). The row stays; only its tokens are replaced.</summary>
/// <param name="Name">The connection name.</param>
/// <param name="Wait">Whether the request waits for the browser callback; false returns the consent URL at once.</param>
public sealed record SignInConnectionRequest(string Name, bool Wait = false) : ICommand<AddConnectionResponse>;
