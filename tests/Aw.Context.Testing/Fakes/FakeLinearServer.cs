using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// A fake Linear GraphQL API on loopback that replays the split-panes recordings (spec §10, D18). 'POST /graphql' answers
/// the root fields projects, documents, projectUpdates and issues (filter, first, after, orderBy) and project, document
/// and issue (by id; an issue also by its identifier). Anything else is a 400 that names what it does serve. The
/// Authorization header must equal <see cref="Token"/> (or 'Bearer ' and it) and is never recorded or logged.
/// </summary>
public sealed class FakeLinearServer : IAsyncDisposable
{
    /// <summary>The credential a server started without options accepts (a fixture value, not a secret).</summary>
    public const string DefaultToken = "fixture-linear";

    private readonly LoopbackServer _server;

    private FakeLinearServer(LoopbackServer server, string token)
    {
        _server = server;
        Token = token;
    }

    /// <summary>The GraphQL endpoint, 'http://127.0.0.1:&lt;port&gt;/graphql'.</summary>
    public Uri Url => new(_server.BaseUrl, "graphql");

    /// <summary>The credential the server accepts.</summary>
    public string Token { get; }

    /// <summary>The requests received, in order (no headers).</summary>
    public IReadOnlyList<RecordedRequest> Requests => _server.Requests;

    /// <summary>Every log line the server and its web host wrote (kept in memory, never printed).</summary>
    public IReadOnlyList<string> Log => _server.Log;

    /// <summary>Starts the server on an ephemeral loopback port.</summary>
    /// <param name="options">Options; null for the defaults.</param>
    /// <returns>The running server.</returns>
    public static async Task<FakeLinearServer> Start(FakeServerOptions? options = null)
    {
        var token = options?.Token ?? DefaultToken;
        var server = await LoopbackServer.Start("fake-linear", r => Handle(r, token)).ConfigureAwait(false);
        return new FakeLinearServer(server, token);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _server.DisposeAsync();

    private static FakeHttpReply Handle(FakeHttpRequest request, string token)
    {
        if (!string.Equals(request.Authorization, token, StringComparison.Ordinal)
            && !string.Equals(request.Authorization, "Bearer " + token, StringComparison.Ordinal))
        {
            return Error(401, "AUTHENTICATION_ERROR", "Authentication required, not authenticated.", "refused: no valid credential");
        }

        if (request.Method != "POST" || request.Path != "/graphql")
        {
            return Error(404, "NOT_FOUND", $"The fake Linear server answers POST /graphql only, not {request.Method} {request.Path}.", "refused: route");
        }

        try
        {
            return Answer(request.Body);
        }
        catch (FakeRequestException e)
        {
            return Error(e.Status, e.Code, e.Message, "refused: " + e.Code);
        }
    }

    private static FakeHttpReply Answer(string body)
    {
        var document = Parse(body);
        var query = document["query"] is JsonValue q && q.GetValueKind() == JsonValueKind.String
            ? q.GetValue<string>()
            : throw FakeRequestException.Invalid("The request has no string 'query'.");
        var fields = GraphQlReader.RootFields(query, document["variables"] as JsonObject);
        var data = new JsonObject();
        var errors = new JsonArray();
        foreach (var field in fields)
        {
            var (value, error) = LinearResolver.Resolve(field);
            data[field.ResponseKey] = value;
            if (error is not null)
            {
                errors.Add(new JsonObject { ["message"] = error, ["path"] = new JsonArray(field.ResponseKey), ["extensions"] = new JsonObject { ["code"] = "INVALID_INPUT" } });
            }
        }

        var reply = new JsonObject { ["data"] = data };
        if (errors.Count > 0)
        {
            reply["errors"] = errors;
        }

        return new FakeHttpReply(200, reply, string.Join(' ', fields.Select(f => f.Name)));
    }

    private static JsonObject Parse(string body)
    {
        try
        {
            return JsonNode.Parse(body) as JsonObject ?? throw FakeRequestException.Invalid("The body isn't a JSON object.");
        }
        catch (JsonException e)
        {
            throw FakeRequestException.Invalid("The body isn't JSON: " + e.Message);
        }
    }

    private static FakeHttpReply Error(int status, string code, string message, string operation) =>
        new(status, new JsonObject { ["errors"] = new JsonArray(new JsonObject { ["message"] = message, ["extensions"] = new JsonObject { ["code"] = code } }) }, operation);
}
