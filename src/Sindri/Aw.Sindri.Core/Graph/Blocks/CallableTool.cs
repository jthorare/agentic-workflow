namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One row of the 'call_tool' picker.</summary>
/// <param name="Tool">What goes in the node's 'tool' field.</param>
/// <param name="Connection">What goes in the node's 'connection' field; null for a toolkit tool.</param>
/// <param name="Description">What the tool does.</param>
/// <param name="Source">'toolkit', 'mcp' or 'rest'.</param>
public sealed record CallableTool(string Tool, string? Connection, string Description, string Source);
