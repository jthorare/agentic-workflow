using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where a 'call_tool' node reaches a connection: the tools of an 'mcp' connection and the actions of a 'rest' connection's preset.</summary>
public interface IConnectionToolPort
{
    /// <summary>Lists what a connection offers.</summary>
    /// <param name="connection">The connection's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tools, or SND-CONN-001 when no connection has that name.</returns>
    public ValueTask<Result<IReadOnlyList<ConnectionTool>>> ListTools(string connection, CancellationToken cancellationToken);

    /// <summary>Calls one tool or action.</summary>
    /// <param name="connection">The connection's name.</param>
    /// <param name="tool">The MCP tool name or the preset action name.</param>
    /// <param name="argumentsJson">The arguments, a JSON object as text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The JSON result as text, or the connection's own error.</returns>
    public ValueTask<Result<string>> Call(string connection, string tool, string argumentsJson, CancellationToken cancellationToken);
}
