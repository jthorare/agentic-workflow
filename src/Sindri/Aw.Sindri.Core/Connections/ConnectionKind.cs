namespace Aw.Sindri.Core.Connections;

/// <summary>What a connection signs in to. Wire names: 'oauth', 'mcp', 'rest', 'cli' (see <see cref="ConnectionWire"/>).</summary>
public enum ConnectionKind
{
    /// <summary>A provider's OAuth sign-in (PKCE, loopback redirect).</summary>
    OAuth,

    /// <summary>An MCP server (the kind exists in this PR; its client arrives later).</summary>
    Mcp,

    /// <summary>A REST API reached with a key.</summary>
    Rest,

    /// <summary>A command-line tool that reads a credential.</summary>
    Cli,
}
