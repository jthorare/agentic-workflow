using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// A fake Onyx HTTP API on loopback that replays the split-panes recordings (spec §5.2, §10). It answers
/// 'POST /api/query/document-search' (Onyx's DocumentSearchRequest: message, source_type and time_cutoff filters,
/// limit, offset; the reply is 'top_documents' of SearchDocs carrying document_id, metadata and updated_at) and
/// 'GET /api/document/chunk-info' (a chunk's text). Any other route or field is an error that names what it serves.
/// The Authorization header must be 'Bearer ' and <see cref="Token"/>, and is never recorded or logged.
/// </summary>
public sealed class FakeOnyxServer : IAsyncDisposable
{
    /// <summary>The credential a server started without options accepts (a fixture value, not a secret).</summary>
    public const string DefaultToken = "fixture-onyx";

    private const string SearchRoute = "/api/query/document-search";
    private const string ChunkRoute = "/api/document/chunk-info";
    private readonly LoopbackServer _server;

    private FakeOnyxServer(LoopbackServer server, string token)
    {
        _server = server;
        Token = token;
    }

    /// <summary>The base URL, 'http://127.0.0.1:&lt;port&gt;/'.</summary>
    public Uri Url => _server.BaseUrl;

    /// <summary>The credential the server accepts.</summary>
    public string Token { get; }

    /// <summary>The 'Authorization' header value the server accepts: 'Bearer ' and <see cref="Token"/>.</summary>
    public string Authorization => "Bearer " + Token;

    /// <summary>The requests received, in order (no headers).</summary>
    public IReadOnlyList<RecordedRequest> Requests => _server.Requests;

    /// <summary>Every log line the server and its web host wrote (kept in memory, never printed).</summary>
    public IReadOnlyList<string> Log => _server.Log;

    /// <summary>Starts the server on an ephemeral loopback port.</summary>
    /// <param name="options">Options; null for the defaults.</param>
    /// <returns>The running server.</returns>
    public static async Task<FakeOnyxServer> Start(FakeServerOptions? options = null)
    {
        var token = options?.Token ?? DefaultToken;
        var server = await LoopbackServer.Start("fake-onyx", r => Handle(r, "Bearer " + token)).ConfigureAwait(false);
        return new FakeOnyxServer(server, token);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _server.DisposeAsync();

    private static FakeHttpReply Handle(FakeHttpRequest request, string authorization)
    {
        if (!string.Equals(request.Authorization, authorization, StringComparison.Ordinal))
        {
            return Detail(401, "Unauthorized: send 'Authorization: Bearer <api key>'.", "refused: no valid credential");
        }

        try
        {
            return (request.Method, request.Path) switch
            {
                ("POST", SearchRoute) => Search(request.Body),
                ("GET", ChunkRoute) => Chunk(request.Query),
                _ => Detail(404, $"The fake Onyx server has no route {request.Method} {request.Path}; it answers POST {SearchRoute} and GET {ChunkRoute}.", "refused: route"),
            };
        }
        catch (FakeRequestException e)
        {
            return new FakeHttpReply(e.Status, new JsonObject { ["detail"] = e.Detail?.DeepClone() ?? e.Message }, "refused: " + e.Code);
        }
    }

    private static FakeHttpReply Search(string body)
    {
        var request = OnyxSearchRequest.Parse(Parse(body));
        var documents = OnyxIndex.Search(request);
        return new FakeHttpReply(200, new JsonObject { ["top_documents"] = documents, ["llm_indices"] = new JsonArray() }, "document-search");
    }

    private static FakeHttpReply Chunk(IReadOnlyDictionary<string, string> query)
    {
        var id = query.GetValueOrDefault("document_id")
            ?? throw OnyxSearchRequest.Unprocessable(["query", "document_id"], "Field required.", "missing");
        var chunk = int.TryParse(query.GetValueOrDefault("chunk_id"), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n
            : throw OnyxSearchRequest.Unprocessable(["query", "chunk_id"], "Field required: a non-negative integer.", "int_parsing");
        var document = OnyxIndex.Documents.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal));
        if (document is null || chunk >= document.Chunks.Count)
        {
            return Detail(404, string.Create(CultureInfo.InvariantCulture, $"Document chunk not found: {id} chunk {chunk}."), "chunk-info");
        }

        var text = document.Chunks[chunk];
        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return new FakeHttpReply(200, new JsonObject { ["content"] = text, ["num_tokens"] = tokens }, "chunk-info");
    }

    private static JsonObject Parse(string body)
    {
        try
        {
            return JsonNode.Parse(body) as JsonObject ?? throw OnyxSearchRequest.Unprocessable(["body"], "Must be a JSON object.", "model_type");
        }
        catch (JsonException e)
        {
            throw OnyxSearchRequest.Unprocessable(["body"], "Invalid JSON: " + e.Message, "json_invalid");
        }
    }

    private static FakeHttpReply Detail(int status, string detail, string operation) =>
        new(status, new JsonObject { ["detail"] = detail }, operation);
}
