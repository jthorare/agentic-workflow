using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// IKeyCheck over one HTTPS call per service (#4357). Linear: a POST of `{ viewer { id } }` to its GraphQL endpoint with the key as it is in the Authorization header (a personal API key takes no
/// 'Bearer'). Slack: a GET of the preset's probe URL (auth.test), which answers 200 with `"ok": false` for a bad token. Any other preset with a probe URL: a GET with a Bearer header, the status decides.
/// A preset with neither is not checked. The key goes only into the Authorization header; no error holds it or the body.
/// </summary>
/// <param name="http">The HTTP client (no redirects followed).</param>
internal sealed class HttpKeyCheck(HttpClient http) : IKeyCheck
{
    /// <summary>Linear's GraphQL endpoint.</summary>
    internal const string LinearGraphQl = "https://api.linear.app/graphql";

    private const string ViewerQuery = """{"query":"{ viewer { id } }"}""";

    public async ValueTask<Result<Unit>> Check(ConnectionPreset preset, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentException.ThrowIfNullOrEmpty(key);
        using var request = Request(preset, key);
        if (request is null)
        {
            return Unit.Value;
        }

        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
            {
                return Refused(preset);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Unchecked(preset);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Accepted(preset.Key, body) ? Unit.Value : Refused(preset);
        }
        catch (HttpRequestException)
        {
            return Unchecked(preset);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unchecked(preset);
        }
    }

    // The one request for the preset's service, or null when the preset has no check.
    private static HttpRequestMessage? Request(ConnectionPreset preset, string key)
    {
        HttpRequestMessage request;
        if (string.Equals(preset.Key, "linear", StringComparison.Ordinal))
        {
            request = new HttpRequestMessage(HttpMethod.Post, LinearGraphQl) { Content = new StringContent(ViewerQuery, Encoding.UTF8, "application/json") };
            _ = request.Headers.TryAddWithoutValidation("Authorization", key);
        }
        else if (preset.ProbeUrl is { } probe)
        {
            request = new HttpRequestMessage(HttpMethod.Get, probe);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
        else
        {
            return null;
        }

        request.Headers.UserAgent.ParseAdd("sindri");   // GitHub refuses a request with no User-Agent
        return request;
    }

    // A 2xx is enough for most services; Slack says no inside a 200, Linear names no viewer when the key is wrong.
    private static bool Accepted(string preset, string body)
    {
        var slack = string.Equals(preset, "slack", StringComparison.Ordinal);
        if (!slack && !string.Equals(preset, "linear", StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object && (slack ? SlackOk(document.RootElement) : LinearViewer(document.RootElement));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool SlackOk(JsonElement root) => root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;

    private static bool LinearViewer(JsonElement root) =>
        root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty("viewer", out var viewer) && viewer.ValueKind == JsonValueKind.Object
        && viewer.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String;

    private static Error Refused(ConnectionPreset preset) =>
        SindriErrors.ConnectionKeyRefused.With($"{preset.DisplayName} refused the key; nothing was stored (fix: check it was copied whole and has not expired or been revoked, then paste it again)");

    private static Error Unchecked(ConnectionPreset preset) =>
        SindriErrors.ConnectionKeyUnchecked.With($"Sindri could not reach {preset.DisplayName} to check the key; nothing was stored (fix: check the network and try again)");
}
