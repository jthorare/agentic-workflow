using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// Reads the team keys a Linear API key can see: one 'POST' of a fixed GraphQL query to the profile's Linear url, on a handler that follows no redirect and uses no proxy. The key goes only into the Authorization header
/// (a personal API key is sent as it is, without 'Bearer'), never into an error. Failures map to the catalogue as Context's Linear client does: 401, 403 or an authentication error 071, no answer, a 5xx or a rate limit 010, any other shape 072.
/// </summary>
/// <param name="url">The GraphQL endpoint (the profile's checked url).</param>
/// <param name="key">The API key.</param>
/// <param name="http">The client (production: one shared client).</param>
internal sealed class LinearTeamReader(Uri url, string key, HttpClient http) : ILinearTeamReader
{
    private const string Query = """{"query":"query { teams(first: 250) { nodes { key } } }"}""";
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(15);

    private static readonly HttpClient _shared = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });

    /// <summary>Makes the reader 'index connect linear' checks a key with.</summary>
    /// <param name="url">The GraphQL endpoint.</param>
    /// <param name="key">The API key.</param>
    /// <returns>The reader.</returns>
    internal static ILinearTeamReader Create(Uri url, string key) => new LinearTeamReader(url, key, _shared);

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<string>>> Teams(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_deadline);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(Query, Encoding.UTF8) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            _ = request.Headers.TryAddWithoutValidation("Authorization", key);
            using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
            return Read(response.StatusCode, body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ContextErrors.SourceUnavailable.With($"Linear at {url.Authority} didn't answer within {_deadline.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s; nothing was saved.");
        }
        catch (HttpRequestException e)
        {
            return ContextErrors.SourceUnavailable.With($"Linear at {url.Authority} isn't reachable ({e.HttpRequestError}); nothing was saved (fix: check context.sources.linear.url and your network).");
        }
    }

    private Result<IReadOnlyList<string>> Read(HttpStatusCode status, byte[] body)
    {
        var code = (int)status;
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ContextErrors.LinearKeyRefused.With($"Linear at {url.Authority} refused the key.");
        }

        if (status == HttpStatusCode.TooManyRequests || code >= 500)
        {
            return ContextErrors.SourceUnavailable.With($"Linear at {url.Authority} answered HTTP {code.ToString(CultureInfo.InvariantCulture)}; nothing was saved (fix: run `sindri index connect linear` again in a minute).");
        }

        return Shape(code, body);
    }

    private Result<IReadOnlyList<string>> Shape(int code, byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (GraphQlError(root) is { } error)
            {
                return error;
            }

            if (code is >= 200 and < 300 && NodesOf(root) is { } nodes)
            {
                return nodes.EnumerateArray()
                    .Select(static n => n.ValueKind == JsonValueKind.Object && n.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null)
                    .OfType<string>()
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // falls through to the unexpected-shape answer below
        }

        return ContextErrors.LinearUnexpectedReply.With($"Linear at {url.Authority} answered the team read (HTTP {code.ToString(CultureInfo.InvariantCulture)}) with a body this build does not know; nothing was saved.");
    }

    private Error? GraphQlError(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0)
        {
            return null;
        }

        return IsAuthError(errors)
            ? ContextErrors.LinearKeyRefused.With($"Linear at {url.Authority} refused the key.")
            : ContextErrors.LinearUnexpectedReply.With($"Linear at {url.Authority} answered the team read with a GraphQL error; nothing was saved.");
    }

    private static JsonElement? NodesOf(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty("teams", out var teams) && teams.ValueKind == JsonValueKind.Object
        && teams.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array
            ? nodes
            : null;

    private static bool IsAuthError(JsonElement errors) =>
        errors.EnumerateArray().Any(static e =>
            e.ValueKind == JsonValueKind.Object
            && e.TryGetProperty("extensions", out var ext) && ext.ValueKind == JsonValueKind.Object
            && ext.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
            && (string.Equals(c.GetString(), "AUTHENTICATION_ERROR", StringComparison.Ordinal) || string.Equals(c.GetString(), "FORBIDDEN", StringComparison.Ordinal)));
}
