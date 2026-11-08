using Aw.Kernel;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// IMcpConnectionClient over ModelContextProtocol.Core: connects to the server with the credential as a bearer header and lists its tools. The error text names the
/// status or the transport failure, never the credential or the server's body. (Its OAuth side, discovery and RFC 7591 registration, is <see cref="LoopbackSignIn.StartMcp"/>'s.)
/// </summary>
/// <param name="http">The HTTP client the transport uses (tests point it at the fake MCP server).</param>
internal sealed class McpConnectionClient(HttpClient http) : IMcpConnectionClient
{
    private readonly HttpClient _http = http;

    public ValueTask<Result<IReadOnlyList<string>>> ListTools(Uri server, TokenSet tokens, CancellationToken cancellationToken)
    {
        _ = (_http, server, tokens, cancellationToken);
        throw new NotSupportedException();
    }
}
