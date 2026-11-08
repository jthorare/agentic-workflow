using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetConnection;

/// <summary>One connection ('sindri connection show &lt;name&gt;'; GET /sindri/connections/{name}; MCP connection_get).</summary>
/// <param name="Name">The connection name.</param>
public sealed record GetConnectionRequest(string Name) : IQuery<GetConnectionResponse>;
