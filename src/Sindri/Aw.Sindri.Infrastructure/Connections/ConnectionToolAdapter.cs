using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IConnectionToolPort over the connection store: an 'mcp' connection's tools through the MCP client, a preset's rest actions through a signed request.</summary>
internal sealed class ConnectionToolAdapter : IConnectionToolPort
{
    public ConnectionToolAdapter(IConnectionStore store, IConnectionSecrets secrets, IMcpConnectionClient mcp, HttpClient http, ITextScrubber scrubber)
    {
        _ = (store, secrets, mcp, http, scrubber);
    }

    public ValueTask<Result<IReadOnlyList<ConnectionTool>>> ListTools(string connection, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<string>> Call(string connection, string tool, string argumentsJson, CancellationToken cancellationToken) => throw new NotSupportedException();
}
