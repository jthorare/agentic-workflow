namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One thing a connection can be asked to do.</summary>
/// <param name="Name">The MCP tool name or the preset action name.</param>
/// <param name="Description">What it does.</param>
/// <param name="Source">'mcp' for a tool of an MCP server, 'rest' for an action of a rest preset.</param>
/// <param name="ReadOnly">True when the tool only reads, so a test run may call it for real (Workflows page spec §5.1). False until a mark says otherwise.</param>
public sealed record ConnectionTool(string Name, string Description, string Source, bool ReadOnly = false);
