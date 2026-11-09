namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>Where an MCP server's own sign-in goes (connections PR 10b): the endpoints its metadata named, as a preset the loopback sign-in can use, and the client it registered for Sindri (RFC 7591). It can hold a client secret, so its text shows nothing.</summary>
/// <param name="Preset">The server's authorize and token endpoints as a preset (key 'mcp', no revoke endpoint, no default scopes).</param>
/// <param name="Client">The registered client id (not secret).</param>
/// <param name="ClientSecret">The client secret the registration returned, or null for a public client. Secret.</param>
public sealed record McpSignInTarget(ConnectionPreset Preset, string Client, string? ClientSecret = null)
{
    /// <summary>A fixed text: the client secret is not in it.</summary>
    /// <returns>'McpSignInTarget(redacted)'.</returns>
    public override string ToString() => "McpSignInTarget(redacted)";
}
