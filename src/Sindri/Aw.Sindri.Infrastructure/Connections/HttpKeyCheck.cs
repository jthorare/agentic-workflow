using System.Globalization;
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
/// A preset with neither is not checked. The call waits 15 s at most; a 429, or a 403 that carries Retry-After or `x-ratelimit-remaining: 0` (GitHub's secondary rate limit), is SND-CONN-020 "rate limiting", not a refused key. The key goes only into the Authorization header; no error holds it or the body.
/// </summary>
/// <param name="http">The HTTP client (no redirects followed).</param>
/// <param name="timeout">How long the check waits for the service (default 15 s).</param>
internal sealed class HttpKeyCheck(HttpClient http, TimeSpan? timeout = null) : IKeyCheck
{
    /// <summary>Gets how long the check waits for the service before it answers SND-CONN-019.</summary>
    internal TimeSpan Timeout { get; } = timeout ?? TimeSpan.FromSeconds(15);

    /// <summary>Linear's GraphQL endpoint.</summary>
    internal const string LinearGraphQl = "https://api.linear.app/graphql";

    private const string ViewerQuery = """{"query":"{ viewer { id } }"}""";

    public async ValueTask<Result<KeyCheckOutcome>> Check(ConnectionPreset preset, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentException.ThrowIfNullOrEmpty(key);
        using var request = Request(preset, key);
        if (request is null)
        {
            return KeyCheckOutcome.NotApplicable;
        }

        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timer.CancelAfter(Timeout);
        try
        {
            using var response = await http.SendAsync(request, timer.Token).ConfigureAwait(false);
            if (RateLimited(response))
            {
                return Limited(preset);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
            {
                return Refused(preset);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Unchecked(preset);
            }

            var body = await response.Content.ReadAsStringAsync(timer.Token).ConfigureAwait(false);
            return Accepted(preset.Key, body) ? KeyCheckOutcome.Checked : Refused(preset);
        }
        catch (HttpRequestException)
        {
            return Unchecked(preset);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.ConnectionKeyUnchecked.With($"{preset.DisplayName} did not answer in {Timeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s, so the key was not checked; nothing was stored (fix: check the network and try again)");
        }
    }

    // A 429, or GitHub's secondary rate limit: a 403 with Retry-After or no requests left. A plain 403 stays a refused key.
    private static bool RateLimited(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.TooManyRequests
        || (response.StatusCode == HttpStatusCode.Forbidden
            && (response.Headers.RetryAfter is not null
                || (response.Headers.TryGetValues("x-ratelimit-remaining", out var left) && left.Any(static v => string.Equals(v.Trim(), "0", StringComparison.Ordinal)))));

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

    private static Error Limited(ConnectionPreset preset) =>
        SindriErrors.ConnectionKeyRateLimited.With($"{preset.DisplayName} is rate limiting Sindri, so the key was not checked; nothing was stored (fix: wait a minute and try again)");

    private static Error Unchecked(ConnectionPreset preset) =>
        SindriErrors.ConnectionKeyUnchecked.With($"Sindri could not reach {preset.DisplayName} to check the key; nothing was stored (fix: check the network and try again)");
}
