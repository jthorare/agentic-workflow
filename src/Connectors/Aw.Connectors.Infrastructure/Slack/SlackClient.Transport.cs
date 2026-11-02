using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Connectors.Core;
using Aw.Kernel;

namespace Aw.Connectors.Infrastructure.Slack;

/// <summary>The transport of <see cref="SlackClient"/>: one GET, its status and its ok flag mapped to a value or an error, the rate limit, the body parsed.</summary>
internal sealed partial class SlackClient
{
    private const int DefaultWaitSeconds = 60;

    private async ValueTask<Result<Step>> Fetch(Uri url, CancellationToken cancellationToken)
    {
        if (!SlackHostPolicy.IsAllowed(url))
        {
            return ConnectorsErrors.HostRefused.With("Only https on slack.com is requested.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("agentic-workflow-sindri");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await Interpret(response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            return ConnectorsErrors.Unreachable.With($"Slack could not be reached ({e.HttpRequestError}).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConnectorsErrors.Unreachable.With("Slack did not answer in time.");
        }
    }

    private async ValueTask<Result<Step>> Interpret(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        return status switch
        {
            >= 300 and < 400 => ConnectorsErrors.RedirectRefused.With(string.Create(CultureInfo.InvariantCulture, $"Slack answered {status}; redirects are never followed.")),
            429 => new Step(Body: null, RateLimitedUntil: RateLimitReset(response)),
            200 => await Parse(response, cancellationToken).ConfigureAwait(false),
            401 => ConnectorsErrors.Unauthorized.With("Slack rejected the token; check context.sources.slack.token."),
            403 => ConnectorsErrors.AccessDenied.With("Slack refused the request; the token may lack read access to the channel."),
            _ => ConnectorsErrors.BadReply.With(string.Create(CultureInfo.InvariantCulture, $"Slack answered {status}.")),
        };
    }

    private async ValueTask<Result<Step>> Parse(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ConnectorsErrors.BadReply.With("Slack's reply was not a JSON object.");
            }

            return root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True
                ? new Step(root.Clone(), RateLimitedUntil: null)
                : NotOk(SlackWire.ErrorSlug(root));
        }
        catch (JsonException)
        {
            return ConnectorsErrors.BadReply.With("Slack's reply was not valid JSON.");
        }
    }

    /// <summary>Maps the error slug of an ok:false answer. The slug is a short lowercase word Slack documents, so it can be shown; nothing else of the body is.</summary>
    private Result<Step> NotOk(string slug)
    {
        var said = $"Slack answered ok:false ({slug}).";
        return slug switch
        {
            "ratelimited" or "rate_limited" => new Step(Body: null, RateLimitedUntil: _clock.GetUtcNow().AddSeconds(DefaultWaitSeconds)),
            "invalid_auth" or "not_authed" or "token_revoked" or "token_expired" or "account_inactive" => ConnectorsErrors.Unauthorized.With(said + " Check context.sources.slack.token."),
            "missing_scope" or "not_in_channel" or "access_denied" or "restricted_action" or "no_permission" => ConnectorsErrors.AccessDenied.With(said + " The bot needs channels:history (and groups:history for a private channel) and must be in the channel."),
            "channel_not_found" or "thread_not_found" or "message_not_found" => ConnectorsErrors.NotFound.With(said),
            _ => ConnectorsErrors.BadReply.With(said),
        };
    }

    private DateTimeOffset RateLimitReset(HttpResponseMessage response)
        => _clock.GetUtcNow().AddSeconds(
            int.TryParse(SlackWire.Header(response, "Retry-After"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ? seconds : DefaultWaitSeconds);

    private sealed record Step(JsonElement? Body, DateTimeOffset? RateLimitedUntil);
}
