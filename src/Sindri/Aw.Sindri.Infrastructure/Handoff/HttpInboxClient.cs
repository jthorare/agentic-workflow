using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Handoff;

namespace Aw.Sindri.Infrastructure.Handoff;

/// <summary>
/// IInboxClient over the three HTTP calls of schemas/handoff/inbox.openapi.yaml. The inbox's host has to be on the allowlist, and its URL https (http only for
/// loopback). Each attempt has a 30 s limit. List and submit are tried up to three times on a transport failure, a timeout, a 429 or a 5xx; claim is tried once,
/// because a claim that timed out may have won and a second one would lose to it. A redirect is a failure (the allowlist would not cover where it goes). The
/// errors name a status or a field, never the key and never the inbox's text.
/// </summary>
/// <param name="http">The HTTP client (it should not follow redirects).</param>
/// <param name="allowedHosts">The hosts an inbox may be on.</param>
/// <param name="retryDelay">How long list and submit wait before a retry.</param>
[SuppressMessage("Meziantou.Analyzer", "MA0182:Internal type is apparently never used", Justification = "The inbox poller (the next change in this series) builds it; until then only the tests do.")]
internal sealed class HttpInboxClient(HttpClient http, IReadOnlyCollection<string> allowedHosts, TimeSpan retryDelay) : IInboxClient
{
    private const int Attempts = 3;
    private const int BodyLimit = 4_000_000;
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http = http;
    private readonly IReadOnlyCollection<string> _allowedHosts = allowedHosts;
    private readonly TimeSpan _retryDelay = retryDelay;

    public async ValueTask<Result<OfferPage>> ListOffers(InboxTarget target, string? pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Guard(target) is { } refused)
        {
            return refused;
        }

        var query = "?owner=" + Uri.EscapeDataString(target.Owner) + (pageToken is null ? string.Empty : "&pageToken=" + Uri.EscapeDataString(pageToken));
        var answer = await Send(new Call(HttpMethod.Get, Url(target, "offers" + query), Body: null, Attempts), target, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        var (status, body) = answer.Value;
        return status == HttpStatusCode.OK ? InboxWire.ParsePage(body) : Unexpected(status);
    }

    public async ValueTask<Result<ClaimResult>> Claim(InboxTarget target, string offerKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Guard(target) is { } refused)
        {
            return refused;
        }

        var answer = await Send(new Call(HttpMethod.Post, Url(target, "offers/" + Uri.EscapeDataString(offerKey) + "/claim"), InboxWire.ClaimBody(target.Owner), Attempts: 1), target, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        var (status, body) = answer.Value;
        if (status == HttpStatusCode.OK)
        {
            return InboxWire.ParseClaim(body);
        }

        if (status == HttpStatusCode.Conflict)
        {
            return SindriErrors.InboxClaimLost.With("another caller claimed the offer first");
        }

        return status is HttpStatusCode.NotFound or HttpStatusCode.Gone
            ? SindriErrors.InboxOfferGone.With("the inbox answered " + Code(status) + ": the offer is not there")
            : Unexpected(status);
    }

    public async ValueTask<Result<Unit>> Submit(InboxTarget target, string offerKey, InboxResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(result);
        if (Guard(target) is { } refused)
        {
            return refused;
        }

        var answer = await Send(new Call(HttpMethod.Post, Url(target, "offers/" + Uri.EscapeDataString(offerKey) + "/result"), InboxWire.ResultBody(result), Attempts), target, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error.Code == SindriErrors.InboxUnavailable.Code ? SindriErrors.InboxSubmitFailed.With(answer.Error.Message) : answer.Error;
        }

        var status = answer.Value.Status;
        return status == HttpStatusCode.OK ? Results.Ok() : SindriErrors.InboxSubmitFailed.With("the inbox answered " + Code(status) + " to the result");
    }

    private static Uri Url(InboxTarget target, string relative) => new(target.BaseUrl.AbsoluteUri.TrimEnd('/') + "/" + relative);

    private static string Code(HttpStatusCode status) => ((int)status).ToString(CultureInfo.InvariantCulture);

    private static Error Unexpected(HttpStatusCode status) => SindriErrors.InboxUnavailable.With("the inbox answered " + Code(status));

    private Error? Guard(InboxTarget target)
    {
        var uri = target.BaseUrl;
        var secure = uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
        return secure && _allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
            ? null
            : SindriErrors.InboxHostNotAllowed.With("the inbox's host is not on the allowlist, or its URL is not https");
    }

    // One call with up to Call.Attempts tries: a refused key or an answer the client can use ends it at once; a transport failure, a timeout, a 429 or a 5xx is tried again after the delay.
    private async ValueTask<Result<(HttpStatusCode Status, string Body)>> Send(Call call, InboxTarget target, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var (final, reason) = await Once(call, target, cancellationToken).ConfigureAwait(false);
            if (final is { } done)
            {
                return done;
            }

            if (attempt >= call.Attempts)
            {
                return SindriErrors.InboxUnavailable.With(reason);
            }

            await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    // One try. Final is set when the call is over (an answer, a refused key, a redirect); it is null, with the reason, when the try may be repeated.
    private async ValueTask<(Result<(HttpStatusCode Status, string Body)>? Final, string Reason)> Once(Call call, InboxTarget target, CancellationToken cancellationToken)
    {
        try
        {
            using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limited.CancelAfter(_limit);
            using var request = Build(call, target);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limited.Token).ConfigureAwait(false);
            var status = response.StatusCode;
            if (status == HttpStatusCode.TooManyRequests || (int)status >= 500)
            {
                return (null, "the inbox answered " + Code(status));
            }

            return (await Settle(response, limited.Token).ConfigureAwait(false), string.Empty);
        }
        catch (HttpRequestException)
        {
            return (null, "the inbox could not be reached");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, "the inbox did not answer in 30 seconds");
        }
    }

    private static HttpRequestMessage Build(Call call, InboxTarget target)
    {
        var request = new HttpRequestMessage(call.Method, call.Uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", target.Credential.AccessToken);
        if (call.Body is not null)
        {
            request.Content = new ByteArrayContent(call.Body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return request;
    }

    // The answers that end a call at once: a refused key, a redirect (not followed: the allowlist would not cover where it goes), or a body to hand to the caller.
    private static async ValueTask<Result<(HttpStatusCode Status, string Body)>> Settle(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = response.StatusCode;
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return SindriErrors.ConnectionNeedsSignIn.With("the inbox refused the key");
        }

        if ((int)status is >= 300 and < 400)
        {
            return SindriErrors.InboxUnavailable.With("the inbox answered a redirect, which is not followed");
        }

        var text = response.Content.Headers.ContentLength > BodyLimit ? null : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return text is null || text.Length > BodyLimit
            ? SindriErrors.InboxContractViolation.With("the inbox's answer is too large")
            : (status, text);
    }

    private readonly record struct Call(HttpMethod Method, Uri Uri, byte[]? Body, int Attempts);
}
