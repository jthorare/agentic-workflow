using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// Finds the OAuth endpoints of an MCP server and registers Sindri as a client there: the server's protected-resource metadata (RFC 9728) names its authorization
/// server, that server's metadata (RFC 8414) names the endpoints, and its registration endpoint takes the client (RFC 7591) with the loopback redirect. Every endpoint must be
/// https, or http on a loopback address. A failure names the step, never what the server sent.
/// </summary>
/// <param name="http">The HTTP client.</param>
internal sealed class McpOAuthDiscovery(HttpClient http)
{
    private readonly HttpClient _http = http;

    internal async ValueTask<Result<McpSignInTarget>> Register(Uri server, string redirectUri, CancellationToken cancellationToken)
    {
        var metadata = await Metadata(server, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return Refused("the MCP server publishes no OAuth metadata (fix: give the connection a key with --key-env instead)");
        }

        if (Text(metadata.Value, "authorization_endpoint") is not { } authorize || Text(metadata.Value, "token_endpoint") is not { } token || !Allowed(authorize) || !Allowed(token))
        {
            return Refused("the MCP server's OAuth metadata has no usable authorize and token endpoints (fix: give the connection a key with --key-env instead)");
        }

        if (Text(metadata.Value, "registration_endpoint") is not { } registration || !Allowed(registration))
        {
            return Refused("the MCP server does not let clients register themselves (fix: give the connection a key with --key-env instead)");
        }

        var client = await Client(registration, redirectUri, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return Refused("the MCP server did not accept Sindri as a client");
        }

        var preset = new ConnectionPreset("mcp", server.Host, ConnectionKind.Mcp, authorize, token, RevokeUrl: null, DefaultScopes: [], HostGroup: "mcp", LoopbackAllowed: true, SignInNote: string.Empty, PollableEvents: [], ProbeUrl: null);
        return new McpSignInTarget(preset, client.Value.Id, client.Value.Secret);
    }

    private static Error Refused(string text) => SindriErrors.ConnectionRefreshFailed.With(text);

    private static bool Allowed(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && (parsed.Scheme == Uri.UriSchemeHttps || (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback));

    // The server's resource metadata names its authorization server; a server without it is its own.
    private async ValueTask<JsonElement?> Metadata(Uri server, CancellationToken cancellationToken)
    {
        var issuer = new Uri(server.GetLeftPart(UriPartial.Authority));   // no resource metadata: the server's own origin is the authorization server (MCP authorization spec)
        if (await Get(WellKnown(server, "oauth-protected-resource"), cancellationToken).ConfigureAwait(false) is { } resource
            && resource.TryGetProperty("authorization_servers", out var listed)
            && listed.ValueKind == JsonValueKind.Array
            && listed.EnumerateArray().FirstOrDefault().GetString() is { } named
            && Uri.TryCreate(named, UriKind.Absolute, out var authority)
            && Allowed(named))
        {
            issuer = authority;
        }

        return await Get(WellKnown(issuer, "oauth-authorization-server"), cancellationToken).ConfigureAwait(false)
            ?? await Get(WellKnown(issuer, "openid-configuration", withPath: false), cancellationToken).ConfigureAwait(false);
    }

    // RFC 8414 puts the well-known segment between the host and the issuer's path.
    private static Uri WellKnown(Uri issuer, string name, bool withPath = true)
        => new($"{issuer.GetLeftPart(UriPartial.Authority)}/.well-known/{name}{(withPath ? issuer.AbsolutePath.TrimEnd('/') : string.Empty)}");

    private async ValueTask<JsonElement?> Get(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async ValueTask<(string Id, string? Secret)?> Client(string registration, string redirectUri, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["client_name"] = "sindri",
            ["redirect_uris"] = new JsonArray(redirectUri),
            ["grant_types"] = new JsonArray("authorization_code", "refresh_token"),
            ["response_types"] = new JsonArray("code"),
            ["token_endpoint_auth_method"] = "none",
        };
        try
        {
            using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(registration, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            return Text(document.RootElement, "client_id") is { } id ? (id, Text(document.RootElement, "client_secret")) : null;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}
