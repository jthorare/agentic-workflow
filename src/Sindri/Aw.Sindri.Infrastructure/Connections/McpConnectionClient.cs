using System.Net;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// IMcpConnectionClient over ModelContextProtocol.Core: connects to the server with the credential as a bearer header and lists its tools. The error text names the
/// status or the transport failure, never the credential or the server's body. (Its OAuth side, discovery and RFC 7591 registration, is <see cref="LoopbackSignIn.StartMcp"/>'s.)
/// </summary>
/// <param name="http">The HTTP client the transport uses (tests point it at the fake MCP server).</param>
internal sealed class McpConnectionClient(HttpClient http) : IMcpConnectionClient
{
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http = http;

    public async ValueTask<Result<IReadOnlyList<string>>> ListTools(Uri server, TokenSet tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(tokens);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limited.CancelAfter(_limit);
        try
        {
#pragma warning disable CA2000 // the client below owns the transport and is disposed with it
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = server,
                    TransportMode = HttpTransportMode.StreamableHttp,
                    AdditionalHeaders = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer " + tokens.AccessToken },
                },
                _http, loggerFactory: null, ownsHttpClient: false);
#pragma warning restore CA2000
            var options = new McpClientOptions { ClientInfo = new Implementation { Name = "sindri", Version = "1" } };
            var client = await McpClient.CreateAsync(transport, options, loggerFactory: null, limited.Token).ConfigureAwait(false);
            try
            {
                var listed = await client.ListToolsAsync(cancellationToken: limited.Token).ConfigureAwait(false);
                return Results.Success<IReadOnlyList<string>>([.. listed.Select(static t => t.Name).Order(StringComparer.Ordinal)]);
            }
            finally
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is HttpRequestException or McpException or IOException or System.Text.Json.JsonException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return Refused(e);
        }
    }

    // A 401 or 403 anywhere in the chain is the credential's refusal (SND-CONN-002); anything else is the server being out of reach (SND-CONN-005). No message of the exception is copied.
    private static Error Refused(Exception error)
    {
        for (var inner = error; inner is not null; inner = inner.InnerException)
        {
            if (inner is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden })
            {
                return SindriErrors.ConnectionNeedsSignIn.With("the MCP server refused the credential");
            }
        }

        return SindriErrors.ConnectionRefreshFailed.With("the MCP server could not be reached or did not answer as an MCP server");
    }
}
