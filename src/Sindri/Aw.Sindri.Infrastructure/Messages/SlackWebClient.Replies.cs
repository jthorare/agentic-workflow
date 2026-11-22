using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>
/// The reply side of <see cref="SlackWebClient"/>: the HTTP status, then Slack's 'ok' flag and error slug, mapped to the Messages codes (messages spec
/// §10). Only Slack's short documented words reach a message (a slug or a scope name, checked to be plain lowercase), never the body.
/// </summary>
internal sealed partial class SlackWebClient
{
    // Slack's Retry-After when a 429 or 'ratelimited' carries none (guess: Slack documents the header on every 429).
    private static readonly TimeSpan _defaultWait = TimeSpan.FromSeconds(60);

    private async ValueTask<Result<JsonElement>> Interpret(string method, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        return status switch
        {
            200 => await Parse(method, response, cancellationToken).ConfigureAwait(false),
            429 => Hold(method, RetryAfter(response)),
            401 => SindriErrors.MessagesTokenInvalid.With(Said(method, "HTTP 401") + " Make a new token, then sindri messages connect."),
            >= 300 and < 400 => SindriErrors.ConnectionProviderChanged.With(Said(method, Http(status)) + " Redirects are never followed."),
            >= 500 => SindriErrors.MessagesSlackUnreachable.With(Said(method, Http(status)) + " Nothing was read."),
            _ => SindriErrors.ConnectionProviderChanged.With(Said(method, Http(status))),
        };
    }

    private async ValueTask<Result<JsonElement>> Parse(string method, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return SindriErrors.ConnectionProviderChanged.With(Said(method, "a reply that is not a JSON object"));
            }

            return root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True ? root.Clone() : NotOk(method, root);
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionProviderChanged.With(Said(method, "a reply that is not JSON"));
        }
    }

    // An ok:false answer, by its error slug. The admin-gate slugs (019) and 'not_allowed_token_type' (030) are guesses until P0 reads the real ones.
    private Result<JsonElement> NotOk(string method, JsonElement root)
    {
        var slug = Word(root, "error") ?? "unknown";
        var said = Said(method, $"ok:false ({slug})");
        return slug switch
        {
            "ratelimited" or "rate_limited" => Hold(method, _defaultWait),
            "invalid_auth" or "not_authed" or "token_revoked" or "token_expired" or "account_inactive"
                => SindriErrors.MessagesTokenInvalid.With(said + " Make a new token, then sindri messages connect."),
            "missing_scope" => SindriErrors.MessagesScopeMissing.With(
                $"{said} The token needs the scope {Word(root, "needed") ?? "Slack did not name"}: add it in the Slack app, reinstall, reconnect."),
            "team_access_not_granted" or "access_denied" or "ekm_access_denied" or "org_login_required" or "app_restricted"
                => SindriErrors.MessagesAppBlocked.With(said + " Ask the workspace admin to approve the app."),
            "not_allowed_token_type" => SindriErrors.MessagesNotUserToken.With(said + " " + SindriErrors.MessagesNotUserToken.Title + "."),
            _ => SindriErrors.ConnectionProviderChanged.With(said),
        };
    }

    // A 429 (or 'ratelimited'): hold this method only, then answer SND-MSG-005 with the wait.
    private Result<JsonElement> Hold(string method, TimeSpan wait)
    {
        _budget.Hold(method, _clock.GetUtcNow(), wait);
        return RateLimited(method, wait);
    }

    private static Error RateLimited(string method, TimeSpan wait)
    {
        var seconds = (int)Math.Ceiling(wait.TotalSeconds);
        using var value = JsonDocument.Parse(seconds.ToString(CultureInfo.InvariantCulture));
        return SindriErrors.MessagesRateLimited
            .With(string.Create(CultureInfo.InvariantCulture, $"Slack is rate limiting {method}; retry in {seconds} s. Other calls go on."))
            .WithExtension("retryAfter", value.RootElement.Clone());
    }

    private TimeSpan RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => date - _clock.GetUtcNow(),
        _ => _defaultWait,
    };

    private static string Said(string method, string what) => $"Slack's {method} answered {what}.";

    private static string Http(int status) => string.Create(CultureInfo.InvariantCulture, $"HTTP {status}");

    // A short Slack word (an error slug, a scope list such as 'channels:history,im:history'): lowercase letters, digits and _ : . , only, at most 128
    // characters, so nothing a reply makes up reaches a message.
    private static string? Word(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 128 } text
            && text.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or ':' or '.' or ',')
                ? text
                : null;
}
