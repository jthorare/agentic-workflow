using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// The one place a poller sends a request, for one provider host. The token goes only to the preset's host over https, a redirect is refused and never
/// followed, and no error or message holds the token or the response body.
/// </summary>
internal sealed class PollerClient(HttpClient http, IConnectionSecrets secrets, string host, TimeProvider clock, bool bareKeys = false)
{
    internal TimeProvider Clock { get; } = clock;

    internal bool BareKeys { get; } = bareKeys;

    internal async ValueTask<Result<PollerAnswer>> Send(Connection connection, HttpRequestMessage request, bool cursorSent, CancellationToken cancellationToken)
    {
        using (request)
        {
            if (request.RequestUri is not { Scheme: "https" } uri || !string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))
            {
                return SindriErrors.ConnectionProviderChanged.With($"the request would leave {host}; nothing was sent");
            }

            var tokens = connection.SecretPointer is { } pointer
                ? secrets.Read(pointer)
                : SindriErrors.ConnectionNeedsSignIn.With($"{connection.Name} has no stored sign-in");
            if (tokens.IsError)
            {
                return tokens.Error;
            }

            if (BareKeys && connection.Kind != ConnectionKind.OAuth)
            {
                // a personal API key goes bare, as LinearClient sends it
                _ = request.Headers.TryAddWithoutValidation("Authorization", tokens.Value.AccessToken);
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Value.AccessToken);
            }

            request.Headers.UserAgent.ParseAdd("sindri");
            return await Exchange(request, cursorSent, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<PollerAnswer>> Exchange(HttpRequestMessage request, bool cursorSent, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400)
            {
                return SindriErrors.ConnectionProviderChanged.With($"{host} answered a redirect; the token was not sent anywhere else");
            }

            if (IsRateLimited(response, status))
            {
                return new PollerAnswer(Body: null, RateLimited: true, RetryAfter: WaitOf(response));
            }

            return await Classify(response, status, cursorSent, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return new PollerAnswer(Body: null, RateLimited: true, RetryAfter: null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PollerAnswer(Body: null, RateLimited: true, RetryAfter: null);
        }
    }

    private async ValueTask<Result<PollerAnswer>> Classify(HttpResponseMessage response, int status, bool cursorSent, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new PollerAnswer(body, RateLimited: false, RetryAfter: null, NextUrl(response));
        }

        if (status == (int)HttpStatusCode.BadRequest && (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Contains("RATELIMITED", StringComparison.Ordinal))
        {
            return new PollerAnswer(Body: null, RateLimited: true, RetryAfter: WaitOf(response));
        }

        var code = status.ToString(CultureInfo.InvariantCulture);
        return status switch
        {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden => SindriErrors.ConnectionNeedsSignIn.With($"{host} answered {code}"),
            (int)HttpStatusCode.BadRequest or (int)HttpStatusCode.Gone or (int)HttpStatusCode.UnprocessableEntity when cursorSent
                => SindriErrors.ConnectionCursorRejected.With($"{host} answered {code} to the saved cursor"),
            _ => SindriErrors.ConnectionProviderChanged.With($"{host} answered {code}"),
        };
    }

    // 429 and 5xx; GitHub also answers a primary or secondary limit with 403 and either no requests left or a Retry-After.
    private static bool IsRateLimited(HttpResponseMessage response, int status)
        => status is (int)HttpStatusCode.TooManyRequests or >= 500
            || (status == (int)HttpStatusCode.Forbidden && (response.Headers.RetryAfter is not null || Header(response, "x-ratelimit-remaining") == "0"));

    private TimeSpan? WaitOf(HttpResponseMessage response)
    {
        var now = Clock.GetUtcNow();
        TimeSpan? wait = null;
        if (response.Headers.RetryAfter is { } named)
        {
            wait = named.Delta ?? (named.Date - now);
        }
        else if (long.TryParse(Header(response, "x-ratelimit-reset"), NumberStyles.None, CultureInfo.InvariantCulture, out var reset))
        {
            wait = DateTimeOffset.FromUnixTimeSeconds(reset) - now;
        }

        return wait is { } found && found > TimeSpan.Zero ? found : null;
    }

    private static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    // Link: <https://host/path?page=2>; rel="next", <...>; rel="last"
    private static string? NextUrl(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        foreach (var part in values.SelectMany(static v => v.Split(',')))
        {
            var pieces = part.Split(';');
            if (pieces.Length > 1 && pieces.Skip(1).Any(static p => p.Replace(" ", string.Empty, StringComparison.Ordinal) == "rel=\"next\"") && pieces[0].Trim() is ['<', .. var url, '>'])
            {
                return new string(url);
            }
        }

        return null;
    }
}
