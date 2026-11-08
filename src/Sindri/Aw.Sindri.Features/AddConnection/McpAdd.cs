using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>What one mcp add through the server's own sign-in needs.</summary>
/// <param name="Name">The connection.</param>
/// <param name="Server">The MCP server's URL.</param>
/// <param name="Actor">Who is adding.</param>
/// <param name="Wait">Whether the request waits for the callback.</param>
internal sealed record McpAdd(ConnectionName Name, Uri Server, string Actor, bool Wait);
