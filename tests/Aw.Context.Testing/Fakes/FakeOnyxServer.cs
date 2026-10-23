using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// A fake Onyx HTTP API on loopback that replays the split-panes recordings (spec §5.2, §10). Pinned to Onyx
/// <see cref="PinnedVersion"/>: 'GET /api/version' (no key), 'POST /api/admin/search' (admin only),
/// 'GET /api/document/document-size-info' and 'GET /api/document/chunk-info' (any key), 'GET /api/me', the key check
/// (a service-account key 'on_…' raw or as 'Bearer', a personal access token 'onyx_pat_…' as 'Bearer' only; anything
/// else is Onyx's 403) and the error bodies ('{error_code, detail}', Onyx's generic 422) answer as v4.9.0 does. The
/// Authorization header is never recorded or logged.
/// </summary>
public sealed class FakeOnyxServer : IAsyncDisposable
{
    /// <summary>The credential a server started without options accepts: a service-account key (a fixture value, not a secret).</summary>
    public const string DefaultToken = "on_fixture-onyx";

    /// <summary>The Onyx release the fake answers like, and the version 'GET /api/version' reports by default.</summary>
    public const string PinnedVersion = "v4.9.0";

    private const string ServiceAccountPrefix = "on_";
    private const string PersonalTokenPrefix = "onyx_pat_";
    private const string SearchRoute = "/api/admin/search";
    private const string SizeRoute = "/api/document/document-size-info";
    private const string ChunkRoute = "/api/document/chunk-info";
    private const string VersionRoute = "/api/version";
    private const string MeRoute = "/api/me";
    private const string HealthRoute = "/api/health";
    private const string NotAuthenticated = "Access denied. User is not authenticated.";
    private static readonly string[] _accountTypes = ["SERVICE_ACCOUNT", "STANDARD", "BOT", "EXT_PERM_USER"];
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

    /// <summary>The 'Authorization' header value the server accepts for every token kind: 'Bearer ' and <see cref="Token"/>.</summary>
    public string Authorization => "Bearer " + Token;

    /// <summary>The requests received, in order (no headers).</summary>
    public IReadOnlyList<RecordedRequest> Requests => _server.Requests;

    /// <summary>Every log line the server and its web host wrote (kept in memory, never printed).</summary>
    public IReadOnlyList<string> Log => _server.Log;

    /// <summary>Starts the server on an ephemeral loopback port.</summary>
    /// <param name="options">Options; null for the defaults.</param>
    /// <returns>The running server.</returns>
    /// <exception cref="ArgumentException">The token isn't a kind Onyx accepts ('on_…' or 'onyx_pat_…'), or the account type isn't Onyx's.</exception>
    public static async Task<FakeOnyxServer> Start(FakeServerOptions? options = null)
    {
        options ??= new FakeServerOptions();
        var token = options.Token ?? DefaultToken;
        if (!token.StartsWith(ServiceAccountPrefix, StringComparison.Ordinal) && !token.StartsWith(PersonalTokenPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Onyx v4.9.0 accepts only a service-account key ('{ServiceAccountPrefix}…') or a personal access token ('{PersonalTokenPrefix}…'); pass a fixture token with one of those prefixes.",
                nameof(options));
        }

        var account = options.OnyxAccountType ?? "SERVICE_ACCOUNT";
        if (!_accountTypes.Contains(account, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Onyx's account types are {string.Join(", ", _accountTypes)}.", nameof(options));
        }

        var key = new OnyxKey(token, account, options.OnyxAdmin, options.OnyxVersion ?? PinnedVersion, options.OnyxAccountEmail);
        var plant = options.OnyxPlant;
        var documents = options.OnyxDocuments is { } issues ? [.. issues.Select(OnyxIndex.From)] : OnyxIndex.Documents;
        var server = await LoopbackServer.Start("fake-onyx", r => Planted(Handle(r, key, documents), plant), options.Delay).ConfigureAwait(false);
        return new FakeOnyxServer(server, token);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _server.DisposeAsync();

    private static FakeHttpReply Planted(FakeHttpReply reply, Func<string, string>? plant) =>
        plant is null ? reply : reply with { Body = JsonNode.Parse(plant(reply.Body.ToJsonString()))! };   // why: a plant returns JSON text

    private static FakeHttpReply Handle(FakeHttpRequest request, OnyxKey key, IReadOnlyList<OnyxDocument> documents)
    {
        try
        {
            var authenticated = Authenticated(request.Authorization, key.Token);
            return (request.Method, request.Path) switch
            {
                ("GET", HealthRoute) => new FakeHttpReply(200, new JsonObject { ["success"] = true }, "health"),
                ("GET", VersionRoute) => new FakeHttpReply(200, new JsonObject { ["backend_version"] = key.Version }, "version"),
                ("GET", MeRoute) => authenticated ? new FakeHttpReply(200, OnyxMe.Info(key.AccountType, key.Admin, key.Email), "me") : OnyxError(403, "UNAUTHORIZED", "Unauthorized", "refused: no valid credential"),
                ("POST", SearchRoute) or ("GET", SizeRoute) or ("GET", ChunkRoute) when !authenticated => OnyxError(403, "UNAUTHORIZED", NotAuthenticated, "refused: no valid credential"),
                ("POST", SearchRoute) when !key.Admin => OnyxError(403, "INSUFFICIENT_PERMISSIONS", "You do not have the required permissions for this action.", "refused: not admin"),
                ("POST", SearchRoute) => Search(request.Body, documents),
                ("GET", SizeRoute) => Size(request.Query, documents),
                ("GET", ChunkRoute) => Chunk(request.Query, documents),
                _ => OnyxError(404, "NOT_FOUND", $"The fake Onyx server has no route {request.Method} {request.Path}; it answers GET {HealthRoute}, GET {VersionRoute}, GET {MeRoute}, POST {SearchRoute}, GET {SizeRoute} and GET {ChunkRoute}.", "refused: route"),
            };
        }
        catch (FakeRequestException e) when (e.Code == OnyxSearchRequest.ValidationCode)
        {
            return ValidationFailed("refused: " + e.Code);
        }
        catch (FakeRequestException e)
        {
            return OnyxError(e.Status, e.Code, e.Message, "refused: " + e.Code);
        }
    }

    /// <summary>
    /// Onyx's rule (onyx/auth/utils.py get_hashed_bearer_token_from_request): 'Bearer &lt;token&gt;' for any kind, the raw
    /// value only for a service-account key. An unknown key leaves the caller unauthenticated, the same as no key.
    /// </summary>
    private static bool Authenticated(string? authorization, string token) =>
        string.Equals(authorization, "Bearer " + token, StringComparison.Ordinal)
        || (token.StartsWith(ServiceAccountPrefix, StringComparison.Ordinal) && string.Equals(authorization, token, StringComparison.Ordinal));

    private static FakeHttpReply Search(string body, IReadOnlyList<OnyxDocument> documents) =>
        new(200, new JsonObject { ["documents"] = OnyxIndex.Search(OnyxSearchRequest.Parse(body), documents) }, "admin-search");

    private static FakeHttpReply Size(IReadOnlyDictionary<string, string> query, IReadOnlyList<OnyxDocument> documents)
    {
        if (query.GetValueOrDefault("document_id") is not { } id)
        {
            return ValidationFailed("refused: document-size-info query");
        }

        var document = documents.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal));
        return document is null
            ? OnyxError(404, "NOT_FOUND", "Document not found", "document-size-info: not found")
            : new FakeHttpReply(200, new JsonObject { ["num_chunks"] = document.Chunks.Count, ["num_tokens"] = document.Chunks.Sum(Tokens) }, "document-size-info");
    }

    private static FakeHttpReply Chunk(IReadOnlyDictionary<string, string> query, IReadOnlyList<OnyxDocument> documents)
    {
        if (query.GetValueOrDefault("document_id") is not { } id
            || !int.TryParse(query.GetValueOrDefault("chunk_id"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var chunk))
        {
            return ValidationFailed("refused: chunk-info query");
        }

        var document = documents.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal));
        if (document is null || chunk < 0 || chunk >= document.Chunks.Count)
        {
            return OnyxError(404, "NOT_FOUND", "Chunk not found", "chunk-info: not found");
        }

        var text = document.Chunks[chunk];
        return new FakeHttpReply(200, new JsonObject { ["content"] = text, ["num_tokens"] = Tokens(text) }, "chunk-info");
    }

    /// <summary>A stand-in for Onyx's tokenizer count: whitespace-separated words.</summary>
    private static int Tokens(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>An error as Onyx's handlers write it: '{"error_code", "detail"}' (onyx/error_handling, main.py log_http_error).</summary>
    private static FakeHttpReply OnyxError(int status, string code, string detail, string operation) =>
        new(status, new JsonObject { ["error_code"] = code, ["detail"] = detail }, operation);

    /// <summary>Onyx's 422 for a request that fails FastAPI's validation (main.py validation_exception_handler); it names no field.</summary>
    private static FakeHttpReply ValidationFailed(string operation) =>
        new(422, new JsonObject
        {
            ["status_code"] = 422,
            ["message"] = "Request validation failed.",
            ["data"] = null,
            ["error_code"] = "VALIDATION_ERROR",
            ["detail"] = "Request validation failed.",
        }, operation);
}
