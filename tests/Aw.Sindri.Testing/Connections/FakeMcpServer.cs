using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Testing.Connections;

/// <summary>
/// An MCP server on 127.0.0.1 for the mcp client tests (connections PR 10b): POST /mcp speaks JSON-RPC (initialize, notifications/initialized, tools/list) and wants
/// 'Authorization: Bearer &lt;key&gt;'; a wrong or missing key is 401. With an OAuth server it also serves the OAuth metadata
/// (/.well-known/oauth-authorization-server) and a dynamic client registration endpoint (POST /register, RFC 7591), the sign-in page itself being FakeOAuthServer's.
/// </summary>
public sealed class FakeMcpServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Lock _gate = new();
    private readonly List<string> _methods = [];
    private readonly List<string> _registered = [];

    /// <summary>Starts the server on a free loopback port.</summary>
    /// <param name="key">The bearer key it accepts.</param>
    /// <param name="tools">The tool names it lists.</param>
    /// <param name="oauth">The base URL of a FakeOAuthServer whose /authorize and /token the metadata names; null for a server with no sign-in.</param>
    public FakeMcpServer(string key, IReadOnlyList<string> tools, string? oauth = null)
    {
        Key = key;
        Tools = tools;
        OAuth = oauth;
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
        _listener.Prefixes.Add(Url + "/");
        _listener.Start();
        _ = Serve();
    }

    /// <summary>Gets the server's base URL.</summary>
    public string Url { get; }

    /// <summary>Gets the MCP endpoint (the URL a connection stores).</summary>
    public Uri Endpoint => new(Url + "/mcp");

    /// <summary>Gets the key it accepts.</summary>
    public string Key { get; }

    /// <summary>Gets the tool names it lists.</summary>
    public IReadOnlyList<string> Tools { get; }

    /// <summary>Gets the OAuth server its metadata names, or null.</summary>
    public string? OAuth { get; }

    /// <summary>Gets the JSON-RPC methods received, in order.</summary>
    public IReadOnlyList<string> Methods
    {
        get
        {
            lock (_gate)
            {
                return [.. _methods];
            }
        }
    }

    /// <summary>Gets the client names that registered through /register.</summary>
    public IReadOnlyList<string> Registered
    {
        get
        {
            lock (_gate)
            {
                return [.. _registered];
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _listener.Close();

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(CancellationToken.None);
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(CancellationToken.None);
            Route(context, body);
        }
    }

    private void Route(HttpListenerContext context, string body)
    {
        switch (context.Request.Url!.AbsolutePath)
        {
            case "/mcp":
                Mcp(context, body);
                break;
            case "/.well-known/oauth-authorization-server" when OAuth is not null:
                Reply(context, 200, new JsonObject { ["issuer"] = Url, ["authorization_endpoint"] = OAuth + "/authorize", ["token_endpoint"] = OAuth + "/token", ["registration_endpoint"] = Url + "/register", ["code_challenge_methods_supported"] = new JsonArray("S256") });
                break;
            case "/register" when OAuth is not null:
                Register(context, body);
                break;
            default:
                Reply(context, 404, []);
                break;
        }
    }

    private void Register(HttpListenerContext context, string body)
    {
        using var document = JsonDocument.Parse(body);
        var name = document.RootElement.TryGetProperty("client_name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        lock (_gate)
        {
            _registered.Add(name);
        }

        Reply(context, 201, new JsonObject { ["client_id"] = "dcr-client-" + _registered.Count.ToString(CultureInfo.InvariantCulture), ["client_name"] = name });
    }

    private void Mcp(HttpListenerContext context, string body)
    {
        if (context.Request.HttpMethod != "POST")
        {
            Status(context, 405);   // the streamable transport may open a GET stream or send a DELETE; this server has neither
            return;
        }

        if (context.Request.Headers["Authorization"] != "Bearer " + Key)
        {
            Reply(context, 401, new JsonObject { ["error"] = "unauthorized" });
            return;
        }

        var request = JsonNode.Parse(body)!.AsObject();
        var method = request["method"]!.GetValue<string>();
        if (request["id"] is { } unknownId && method is not ("initialize" or "tools/list"))
        {
            MethodNotFound(context, unknownId);   // newer clients try 'server/discover' first and fall back; it is not one of the methods this fake records
            return;
        }

        lock (_gate)
        {
            _methods.Add(method);
        }

        if (request["id"] is not { } id)
        {
            Status(context, 202);   // a notification has no answer
            return;
        }

        JsonNode result = method switch
        {
            "initialize" => new JsonObject { ["protocolVersion"] = request["params"]?["protocolVersion"]?.GetValue<string>() ?? "2025-03-26", ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() }, ["serverInfo"] = new JsonObject { ["name"] = "fake-mcp", ["version"] = "1.0.0" } },
            "tools/list" => new JsonObject { ["tools"] = new JsonArray([.. Tools.Select(static t => (JsonNode)new JsonObject { ["name"] = t, ["description"] = t, ["inputSchema"] = new JsonObject { ["type"] = "object" } })]) },
            _ => [],
        };
        Reply(context, 200, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["result"] = result });
    }

    private static void Status(HttpListenerContext context, int status)
    {
        context.Response.StatusCode = status;
        context.Response.Close();
    }

    private static void MethodNotFound(HttpListenerContext context, JsonNode id)
        => Reply(context, 200, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["error"] = new JsonObject { ["code"] = -32601, ["message"] = "method not found" } });

    private static void Reply(HttpListenerContext context, int status, JsonObject json)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.OutputStream.Write(Encoding.UTF8.GetBytes(json.ToJsonString()));
        context.Response.Close();
    }
}
