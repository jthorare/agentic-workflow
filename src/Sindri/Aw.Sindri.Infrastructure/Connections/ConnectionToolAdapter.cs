using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Infrastructure.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// IConnectionToolPort over the connection store: an 'mcp' connection's tools go through the MCP client, a preset's rest actions (<see cref="RestActions"/>)
/// through the same signed request the pollers use (<see cref="PollerClient"/>: the token goes only to the action's host over https, a redirect is refused).
/// Every text that leaves this class is scrubbed: the stored token itself is cut out first, then Aw.Redaction runs over what is left.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">The connection secrets.</param>
/// <param name="mcp">The MCP client.</param>
/// <param name="http">The HTTP client for rest actions; it must not follow redirects.</param>
/// <param name="scrubber">Scrubs every text that leaves the adapter.</param>
internal sealed class ConnectionToolAdapter(IConnectionStore store, IConnectionSecrets secrets, IMcpConnectionClient mcp, HttpClient http, ITextScrubber scrubber) : IConnectionToolPort
{
    public async ValueTask<Result<IReadOnlyList<ConnectionTool>>> ListTools(string connection, CancellationToken cancellationToken)
    {
        var found = await Find(connection, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value.Kind != ConnectionKind.Mcp)
        {
            return Results.Success<IReadOnlyList<ConnectionTool>>([.. RestActions.For(found.Value.Preset).Select(static a => new ConnectionTool(a.Name, a.Description, "rest"))]);
        }

        var access = Access(found.Value);
        if (access.IsError)
        {
            return access.Error;
        }

        var described = await mcp.Describe(access.Value.Server, access.Value.Tokens, cancellationToken).ConfigureAwait(false);
        return described.IsError
            ? described.Error
            : Results.Success<IReadOnlyList<ConnectionTool>>([.. described.Value.Select(t => new ConnectionTool(t.Name, scrubber.Scrub(t.Description), "mcp"))]);
    }

    public async ValueTask<Result<string>> Call(string connection, string tool, string argumentsJson, CancellationToken cancellationToken)
    {
        var found = await Find(connection, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var tokens = Tokens(found.Value);
        if (tokens.IsError)
        {
            return tokens.Error;
        }

        var answer = found.Value.Kind == ConnectionKind.Mcp
            ? await CallMcp(found.Value, tokens.Value, tool, argumentsJson, cancellationToken).ConfigureAwait(false)
            : await CallRest(found.Value, tool, argumentsJson, cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer.Error : Results.Success(Clean(answer.Value, tokens.Value));
    }

    private async ValueTask<Result<string>> CallMcp(Connection connection, TokenSet tokens, string tool, string argumentsJson, CancellationToken cancellationToken)
    {
        var server = Server(connection);
        return server.IsError ? server.Error : await mcp.CallTool(server.Value, tokens, tool, argumentsJson, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<string>> CallRest(Connection connection, string tool, string argumentsJson, CancellationToken cancellationToken)
    {
        var action = RestActions.For(connection.Preset).FirstOrDefault(a => string.Equals(a.Name, tool, StringComparison.Ordinal));
        if (action is null)
        {
            return SindriErrors.GraphBlockFailed.With($"connection {connection.Name} has no action '{tool}' (fix: list its tools in the call_tool picker)");
        }

        var request = Build(action, argumentsJson);
        if (request.IsError)
        {
            return request.Error;
        }

        var client = new PollerClient(http, secrets, action.Host, TimeProvider.System, action.BareKey);
        var answer = await client.Send(connection, request.Value, cursorSent: false, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        return answer.Value.Body is { } body
            ? body
            : SindriErrors.ConnectionRefreshFailed.With(answer.Value.Reason ?? "the provider did not answer");
    }

    // The path parts come from same-named arguments (url-escaped); the rest of the arguments are the JSON body of a POST or the query of a GET. Nothing is sent when a part is missing.
    private static Result<HttpRequestMessage> Build(RestAction action, string argumentsJson)
    {
        JsonObject arguments;
        try
        {
            arguments = (string.IsNullOrWhiteSpace(argumentsJson) ? [] : JsonNode.Parse(argumentsJson) as JsonObject) ?? throw new JsonException("not an object");
        }
        catch (JsonException)
        {
            return SindriErrors.GraphBlockFailed.With("the arguments are not a JSON object");
        }

        var path = action.Path;
        foreach (var part in PathParts(action.Path))
        {
            if (arguments[part] is not JsonValue value || PathText(value) is not { Length: > 0 } text)
            {
                return SindriErrors.GraphBlockFailed.With($"{action.Name} needs '{part}'");
            }

            path = path.Replace("{" + part + "}", Uri.EscapeDataString(text), StringComparison.Ordinal);
            _ = arguments.Remove(part);
        }

        var url = "https://" + action.Host + path;
        if (action.Method == HttpMethod.Get)
        {
            var query = string.Join('&', arguments.Select(static p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value?.ToString() ?? string.Empty)));
            return new HttpRequestMessage(HttpMethod.Get, query.Length == 0 ? url : url + "?" + query);
        }

        return new HttpRequestMessage(action.Method, url) { Content = new StringContent(arguments.ToJsonString(), Encoding.UTF8, "application/json") };
    }

    // A string is its value (not its JSON text, whose escapes would end up in the url); a number or a bool is its JSON text.
    private static string PathText(JsonValue value) => value.TryGetValue<string>(out var text) ? text : value.ToJsonString();

    private static IEnumerable<string> PathParts(string path)
    {
        for (var open = path.IndexOf('{', StringComparison.Ordinal); open >= 0; open = path.IndexOf('{', open + 1))
        {
            yield return path[(open + 1)..path.IndexOf('}', open)];
        }
    }

    private async ValueTask<Result<Connection>> Find(string connection, CancellationToken cancellationToken)
    {
        var name = ConnectionName.Parse(connection);
        return name.IsError
            ? SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")
            : await store.Get(name.Value, cancellationToken).ConfigureAwait(false);
    }

    private Result<TokenSet> Tokens(Connection connection)
        => connection.SecretPointer is { } pointer
            ? secrets.Read(pointer)
            : SindriErrors.ConnectionNeedsSignIn.With($"{connection.Name} has no stored sign-in");

    private Result<(Uri Server, TokenSet Tokens)> Access(Connection connection)
    {
        var tokens = Tokens(connection);
        var server = Server(connection);
        return tokens.IsError ? tokens.Error : server.IsError ? server.Error : (server.Value, tokens.Value);
    }

    private static Result<Uri> Server(Connection connection)
        => connection.Config.TryGetValue("serverUrl", out var url) && Uri.TryCreate(url, UriKind.Absolute, out var server)
            ? server
            : SindriErrors.InvalidInput.With($"{connection.Name} has no usable server URL");

    // why: the stored token is cut out by value first, so a provider or server that echoes it back is covered even when the pattern scrubber does not know its shape
    private string Clean(string text, TokenSet tokens)
    {
        var cut = string.IsNullOrEmpty(tokens.AccessToken) ? text : text.Replace(tokens.AccessToken, "[redacted]", StringComparison.Ordinal);
        return scrubber.Scrub(cut);
    }
}
