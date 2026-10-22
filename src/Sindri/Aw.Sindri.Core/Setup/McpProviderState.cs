namespace Aw.Sindri.Core.Setup;

/// <summary>One installed provider's line of a repo's MCP item.</summary>
/// <param name="Provider">The provider ('claude', 'codex', 'cursor').</param>
/// <param name="State">Where it stands.</param>
/// <param name="Detail">Why it failed, or null.</param>
public sealed record McpProviderState(string Provider, McpConnection State, string? Detail = null);
