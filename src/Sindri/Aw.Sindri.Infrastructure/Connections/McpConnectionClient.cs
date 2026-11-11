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

    public ValueTask<Result<IReadOnlyList<string>>> ListTools(Uri server, TokenSet tokens, CancellationToken cancellationToken)
        => Use(server, tokens, async (client, token) =>
        {
            var listed = await client.ListToolsAsync(cancellationToken: token).ConfigureAwait(false);
            return Results.Success<IReadOnlyList<string>>([.. listed.Select(static t => t.Name).Order(StringComparer.Ordinal)]);
        }, cancellationToken);

    public ValueTask<Result<IReadOnlyList<McpToolInfo>>> Describe(Uri server, TokenSet tokens, CancellationToken cancellationToken)
        => Use(server, tokens, async (client, token) =>
        {
            var listed = await client.ListToolsAsync(cancellationToken: token).ConfigureAwait(false);
            return Results.Success<IReadOnlyList<McpToolInfo>>([.. listed.Select(static t => new McpToolInfo(t.Name, t.Description ?? string.Empty, t.JsonSchema.GetRawText())).OrderBy(static t => t.Name, StringComparer.Ordinal)]);
        }, cancellationToken);

    public ValueTask<Result<string>> CallTool(Uri server, TokenSet tokens, string tool, string argumentsJson, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(tool);
        var arguments = Arguments(argumentsJson);
        return arguments.IsError
            ? ValueTask.FromResult<Result<string>>(arguments.Error)
            : Use(server, tokens, async (client, token) =>
            {
                var answer = await client.CallToolAsync(tool, arguments.Value, cancellationToken: token).ConfigureAwait(false);
                return answer.IsError == true
                    ? SindriErrors.GraphBlockFailed.With($"the MCP tool '{tool}' reported an error")
                    : Results.Success(Text(answer));
            }, cancellationToken);
    }

    private static Result<Dictionary<string, object?>> Arguments(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                ? document.RootElement.EnumerateObject().ToDictionary(static p => p.Name, static p => (object?)p.Value.Clone(), StringComparer.Ordinal)
                : SindriErrors.GraphBlockFailed.With("the arguments are not a JSON object");
        }
        catch (System.Text.Json.JsonException)
        {
            return SindriErrors.GraphBlockFailed.With("the arguments are not JSON");
        }
    }

    // why: a tool's structured content is already the JSON the next node wants; plain text parts are joined for a tool that only talks
    private static string Text(CallToolResult answer)
        => answer.StructuredContent is { } structured
            ? structured.GetRawText()
            : string.Join('\n', answer.Content.OfType<TextContentBlock>().Select(static c => c.Text));

    private async ValueTask<Result<T>> Use<T>(Uri server, TokenSet tokens, Func<McpClient, CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
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
                return await work(client, limited.Token).ConfigureAwait(false);
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
