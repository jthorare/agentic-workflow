using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListConnections;

/// <summary>Every connection ('sindri connection list'; GET /sindri/connections; MCP connection_list).</summary>
public sealed record ListConnectionsRequest : IQuery<ListConnectionsResponse>;
