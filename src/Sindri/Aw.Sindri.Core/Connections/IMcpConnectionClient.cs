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
}
