namespace Aw.Sindri.Core.Connections;

/// <summary>One tool an MCP server lists.</summary>
/// <param name="Name">The tool's name on the server.</param>
/// <param name="Description">What the server says it does (untrusted text).</param>
/// <param name="InputSchemaJson">The tool's input JSON Schema as text; '{}' when the server gave none.</param>
public sealed record McpToolInfo(string Name, string Description, string InputSchemaJson);
