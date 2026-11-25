using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>Talks to an MCP server on behalf of an 'mcp' connection (connections PR 10b).</summary>
public interface IMcpConnectionClient
{
    /// <summary>Connects to the server with the stored credential and lists its tools: the proof that the credential works.</summary>
    /// <param name="server">The MCP server's URL.</param>
    /// <param name="tokens">The credential (a key from --key-env, or the sign-in's access token).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tool names sorted, or SND-CONN-002 when the server refuses the credential, SND-CONN-005 when it cannot be reached. The text holds no token.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> ListTools(Uri server, TokenSet tokens, CancellationToken cancellationToken);

    /// <summary>Connects and lists each tool with its description and input schema (the call_tool picker's source).</summary>
    /// <param name="server">The MCP server's URL.</param>
    /// <param name="tokens">The credential.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tools sorted by name, or the same errors as <see cref="ListTools"/>.</returns>
    public ValueTask<Result<IReadOnlyList<McpToolInfo>>> Describe(Uri server, TokenSet tokens, CancellationToken cancellationToken);

    /// <summary>Connects and calls one tool.</summary>
    /// <param name="server">The MCP server's URL.</param>
    /// <param name="tokens">The credential.</param>
    /// <param name="tool">The tool's name on that server.</param>
    /// <param name="argumentsJson">The arguments, a JSON object as text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tool's result as text (its structured content as JSON, else its text parts joined), SND-GRAPH-102 when the tool reports an error, or the same errors as <see cref="ListTools"/>. The text holds no token.</returns>
    public ValueTask<Result<string>> CallTool(Uri server, TokenSet tokens, string tool, string argumentsJson, CancellationToken cancellationToken);
}
