using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// <see cref="IDeviceSignIn"/> for GitHub (RFC 8628; first-run spec decision 13, gap 3): POST the preset's device endpoint for a code, then POST its token endpoint with the device_code grant at GitHub's interval.
/// authorization_pending polls again, slow_down adds 5 seconds (or takes the interval GitHub names), expired_token or the code's expiry ends it, access_denied is the owner's no. Both endpoints must be https.
/// The device code and the token are in no error, log or exception; GitHub's error text is not echoed either.
/// </summary>
/// <param name="http">Sends the device and token requests; it must not follow redirects on its own.</param>
/// <param name="clock">Times the polls and the expiry.</param>
internal sealed class GitHubDeviceSignIn(HttpClient http, TimeProvider clock) : IDeviceSignIn
{
    private const string DeviceGrant = "urn:ietf:params:oauth:grant-type:device_code";
    private static readonly TimeSpan _slowDownStep = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _defaultInterval = TimeSpan.FromSeconds(5);
    private static readonly string[] _appBlocked = ["unauthorized_client"];
    private static readonly string[] _clientWrong = ["device_flow_disabled", "incorrect_client_credentials", "unsupported_grant_type"];

    /// <inheritdoc />
    public async ValueTask<Result<DeviceAuthorization>> Start(ConnectionPreset preset, string client, IReadOnlyList<string> scopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(scopes);
        if (Https(preset.DeviceCodeUrl) is not { } endpoint)
        {
            return SindriErrors.InvalidInput.With($"The {preset.DisplayName} preset has no https device endpoint, so it has no device sign-in");
        }

        var asked = scopes.Count > 0 ? scopes : preset.DefaultScopes;
        var answer = await Post(endpoint, [new("client_id", client), new("scope", string.Join(' ', asked))], cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer.Error : Authorization(answer.Value);
    }

    /// <inheritdoc />
    public async ValueTask<Result<TokenSet>> Complete(ConnectionPreset preset, string client, DeviceAuthorization authorization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(authorization);
        if (Https(preset.TokenUrl) is not { } endpoint)
        {
            return SindriErrors.InvalidInput.With($"The {preset.DisplayName} preset's token endpoint is not https");
        }

        var interval = authorization.Interval;
        while (true)
        {
            var left = authorization.ExpiresAt - clock.GetUtcNow();
            if (left <= TimeSpan.Zero)
            {
                return Expired();
            }

            await Task.Delay(interval < left ? interval : left, clock, cancellationToken).ConfigureAwait(false);   // the last poll lands on the expiry, so a yes in the final seconds still counts
            var answer = await Post(endpoint, [new("client_id", client), new("device_code", authorization.DeviceCode), new("grant_type", DeviceGrant)], cancellationToken).ConfigureAwait(false);
            if (answer.IsError)
            {
                return answer.Error;
            }

            var (done, next) = Step(answer.Value, interval);
            if (done is not null)
            {
                return done;
            }

            interval = next;
        }
    }

    // One poll's answer: the tokens, a final error, or the wait before the next poll.
    private (Result<TokenSet>? Done, TimeSpan Interval) Step(JsonElement body, TimeSpan interval)
    {
        if (Text(body, "access_token") is { Length: > 0 } access)
        {
            var expires = Number(body, "expires_in") is { } seconds ? clock.GetUtcNow().AddSeconds(seconds) : (DateTimeOffset?)null;
            return Stop(new TokenSet(access, Text(body, "refresh_token"), expires));
        }

        return Text(body, "error") switch
        {
            "authorization_pending" => (null, interval),
            "slow_down" => (null, Number(body, "interval") is { } named ? TimeSpan.FromSeconds(named) : interval + _slowDownStep),
            "expired_token" => Stop(Expired()),
            "access_denied" => Stop(SindriErrors.ConnectionConsentDenied.With("GitHub's sign-in was denied or cancelled")),
            var other => Stop(Refused(other)),
        };
    }

    private static (Result<TokenSet>? Done, TimeSpan Interval) Stop(Result<TokenSet> done) => (done, TimeSpan.Zero);

    private Result<DeviceAuthorization> Authorization(JsonElement body)
    {
        if (Text(body, "error") is { } error)
        {
            return Refused(error);
        }

        var device = Text(body, "device_code");
        var user = Text(body, "user_code");
        if (device is not { Length: > 0 } || user is not { Length: > 0 } || Https(Text(body, "verification_uri")) is not { } page || Number(body, "expires_in") is not { } expiresIn)
        {
            return SindriErrors.ConnectionProviderChanged.With("GitHub's device code answer is missing a field Sindri needs");
        }

        var interval = Number(body, "interval") is { } seconds ? TimeSpan.FromSeconds(seconds) : _defaultInterval;
        return new DeviceAuthorization(device, user, page, clock.GetUtcNow().AddSeconds(expiresIn), interval);
    }

    private async ValueTask<Result<JsonElement>> Post(Uri endpoint, KeyValuePair<string, string>[] form, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new FormUrlEncodedContent(form) };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("sindri");
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : SindriErrors.ConnectionProviderChanged.With(string.Create(CultureInfo.InvariantCulture, $"GitHub answered {(int)response.StatusCode} with no JSON object"));
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return SindriErrors.ConnectionProviderChanged.With("GitHub's sign-in couldn't be reached: " + e.GetType().Name);
        }
    }

    // GitHub's own error code, from a fixed word list, says why; its description text is not echoed. A wrong client id or an app without Device Flow is the owner's app setup (SND-SETUP-014's family), not an org block.
    private static Error Refused(string? error)
    {
        if (error is not null && _appBlocked.Contains(error, StringComparer.Ordinal))
        {
            return SindriErrors.SetupOauthBlocked.With($"GitHub refused Sindri's app ({error}). Paste a token instead (fix: sindri setup connect github --token-env NAME)");
        }

        return error is not null && _clientWrong.Contains(error, StringComparer.Ordinal)
            ? SindriErrors.SetupNoClientId.With($"GitHub doesn't take this client id ({error}): the id in setup.oauth.github is wrong or the app has no Device Flow. Paste a token instead (fix: sindri setup connect github --token-env NAME)")
            : SindriErrors.ConnectionProviderChanged.With("GitHub's sign-in answered with an error Sindri doesn't know");
    }

    private static Error Expired() => SindriErrors.ConnectionSignInTimeout.With("The code expired before GitHub saw it (fix: get a new code)");

    private static Uri? Https(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps ? parsed : null;

    private static string? Text(JsonElement body, string name) => body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Number(JsonElement body, string name) => body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0 ? number : null;
}
