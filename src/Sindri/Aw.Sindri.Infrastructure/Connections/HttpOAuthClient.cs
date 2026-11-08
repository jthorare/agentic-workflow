using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IOAuthClient over HTTPS form posts to the preset's token and revoke endpoints. A provider's error body never reaches an error message unscrubbed: the message names the status and the provider's error code only.</summary>
/// <param name="http">The HTTP client (tests point it at the fake OAuth server).</param>
/// <param name="time">The clock, for ExpiresAt.</param>
internal sealed class HttpOAuthClient(HttpClient http, TimeProvider time) : IOAuthClient
{
    private readonly HttpClient _http = http;
    private readonly TimeProvider _time = time;

    public async ValueTask<Result<TokenSet>> ExchangeCode(CodeExchange exchange, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = exchange.Code,
            ["redirect_uri"] = exchange.RedirectUri,
            ["client_id"] = exchange.Client,
            ["code_verifier"] = exchange.Verifier,
        };
        var answer = await Post(exchange.Preset.TokenUrl, form, cancellationToken).ConfigureAwait(false);
        return answer.IsError
            ? SindriErrors.ConnectionNeedsSignIn.With($"{exchange.Preset.Key}: the provider did not accept the sign-in code ({answer.Error.Message})")
            : Tokens(answer.Value, previousRefresh: null);
    }

    public async ValueTask<Result<TokenSet>> Refresh(ConnectionPreset preset, string client, string refreshToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = client,
        };
        var answer = await Post(preset.TokenUrl, form, cancellationToken).ConfigureAwait(false);
        return answer.IsError
            ? SindriErrors.ConnectionRefreshFailed.With($"{preset.Key}: {answer.Error.Message}")
            : Tokens(answer.Value, previousRefresh: refreshToken);
    }

    public async ValueTask<Result<Unit>> Revoke(ConnectionPreset preset, string client, string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset.RevokeUrl is null)
        {
            return Unit.Value;
        }

        var form = new Dictionary<string, string>(StringComparer.Ordinal) { ["token"] = token, ["client_id"] = client };
        var answer = await Post(preset.RevokeUrl, form, cancellationToken).ConfigureAwait(false);
        return answer.IsError ? SindriErrors.ConnectionNeedsSignIn.With($"{preset.Key}: the provider would not revoke the token ({answer.Error.Message})") : Unit.Value;
    }

    // Returns the answer's JSON text on a 2xx; otherwise an error naming the status and the provider's error code (a short [a-z_] word) only.
    private async ValueTask<Result<string>> Post(string url, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return body;
            }

            return SindriErrors.ConnectionRefreshFailed.With($"the token endpoint answered {((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)} ({ProviderCode(body)})");
        }
        catch (HttpRequestException)
        {
            return SindriErrors.ConnectionRefreshFailed.With("the token endpoint could not be reached");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.ConnectionRefreshFailed.With("the token endpoint did not answer in time");
        }
    }

    private static string ProviderCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                && error.GetString() is { Length: > 0 and <= 40 } code
                && code.All(static c => char.IsAsciiLetterLower(c) || c == '_'))
            {
                return code;
            }
        }
        catch (JsonException)
        {
            // not JSON: no provider code to name
        }

        return "no error code";
    }

    private Result<TokenSet> Tokens(string json, string? previousRefresh)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (Text(root, "access_token") is not { Length: > 0 } accessToken)
            {
                return SindriErrors.ConnectionRefreshFailed.With("the token endpoint answered without an access token");
            }

            return new TokenSet(accessToken, Text(root, "refresh_token") is { Length: > 0 } next ? next : previousRefresh, ExpiresAt(root));
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionRefreshFailed.With("the token endpoint answered with text that is not JSON");
        }
    }

    private DateTimeOffset? ExpiresAt(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("expires_in", out var seconds) && seconds.ValueKind == JsonValueKind.Number && seconds.TryGetDouble(out var value)
            ? _time.GetUtcNow().AddSeconds(value)
            : null;

    private static string? Text(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
