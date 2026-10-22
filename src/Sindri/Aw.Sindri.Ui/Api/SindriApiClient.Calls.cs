using System.Globalization;
using System.Net;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>One call to Sindri's API (<see cref="SindriApiClient"/>, continued): the headers, the deadline and the answer.</summary>
internal sealed partial class SindriApiClient
{
    private const string TokenRefused = "SND-AUTH-001";

    /// <summary>
    /// The code of "the views are behind the sequence you asked for" (API §2.4): answered at once when the projection is
    /// stalled (it won't move by itself), and after the read waited its 2 s when the views are behind but still moving.
    /// </summary>
    internal const string ViewsBehind = "SND-API-011";

    // The sequence of the last command the API accepted, sent as X-Sindri-After on the view queries (status, services;
    // API §2.4, UI §9.1) until one of them has used it; 0 when there is none to send.
    private long _after;
    private readonly Lock _leaseGate = new();
    private (int Pid, long Epoch)? _lease;

    // How a call differs from a plain one: a view query waits on the pending sequence; a command carries a JSON body.
    private readonly record struct CallOptions(bool View = false, byte[]? Content = null);

    private Task<Result<T>> Get<T>(Uri api, string path, CancellationToken cancellationToken, bool view = false) =>
        Call<T>(api, HttpMethod.Get, path, cancellationToken, new CallOptions(view));

    private async Task<Result<T>> Call<T>(Uri api, HttpMethod method, string path, CancellationToken cancellationToken, CallOptions options = default)
    {
        var (view, content) = options;
        using var timeout = new CancellationTokenSource(CallTimeout, clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var (code, body) = await Exchange(method, new Uri(api, path), view, content, deadline.Token).ConfigureAwait(false);
            if (Refusal(code, body) is { } refused)
            {
                return refused;
            }

            return code is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices
                ? SindriUiErrors.BadStatus.With(string.Create(CultureInfo.InvariantCulture, $"{path} answered {(int)code}"))
                : Read<T>(path, body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriUiErrors.Unreachable.With(string.Create(CultureInfo.InvariantCulture, $"{api.Authority} didn't answer within {CallTimeout.TotalSeconds:0} s"));
        }
        catch (HttpRequestException)
        {
            // The platform's message repeats the address in its own words ("Connection refused (127.0.0.1:61879)"): I7.
            return SindriUiErrors.Unreachable.With($"nothing answered at {api.Authority}");
        }
    }

    // Another process or epoch is another ledger (the lease lives in it): a sequence from the old one means nothing to
    // the new, so it is dropped when ping shows the change. The first ping has nothing to compare with.
    private void Lease(PingView ping)
    {
        var seen = (ping.Pid, ping.Epoch);
        lock (_leaseGate)
        {
            if (_lease is { } before && before != seen)
            {
                Volatile.Write(ref _after, 0);
            }

            _lease = seen;
        }
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _unreadable = new(StringComparer.Ordinal);

    private Result<T> Read<T>(string path, byte[] body)
    {
        var value = AwJson.Deserialize<T>(body, Json);
        if (value.IsError)
        {
            // The parser's text names .NET types, JSON paths and byte positions: the log keeps it, the window gets the
            // route in words (UI spec §4.1 Unreadable; as I7 for SND-UI-002).
            // The shell reads again on every event: the same failure for a route is logged once, until the route reads well.
            if (!string.Equals(_unreadable.GetValueOrDefault(path), value.Error.Message, StringComparison.Ordinal))
            {
                _unreadable[path] = value.Error.Message;
                SindriApiLog.Unreadable(_log, path, value.Error.Message);
            }

            return SindriUiErrors.BadAnswer.With($"{path} answered with something this build can't read");
        }

        _ = _unreadable.TryRemove(path, out _);

        // An accepted command (CreateItem nests it) is waited for by the next query.
        if ((object?)value.Value switch { CommandAcceptedView a => a.Sequence, ItemCreatedView c => c.Accepted.Sequence, RequestFiledView f => f.Accepted.Sequence, RecipeApproved r => r.Accepted.Sequence, _ => (long?)null } is { } sequence)
        {
            _ = Interlocked.Exchange(ref _after, sequence);
        }
        else if (value.Value is PingView ping)
        {
            Lease(ping);
        }

        return value;
    }

    // A call, and its one retry when the token was stale.
    // A view query carries the pending sequence on both tries, and has used it once it got any answer, SND-API-011
    // included: read-your-writes is best effort (API §2.4), so the next read goes without it and shows what Sindri says
    // (a stalled projection would otherwise answer "behind" for ever).
    private async Task<(HttpStatusCode Code, byte[] Body)> Exchange(HttpMethod method, Uri url, bool view, byte[]? content, CancellationToken cancellationToken)
    {
        var after = view ? Volatile.Read(ref _after) : 0;
        var answer = await Send(method, url, after, content, cancellationToken).ConfigureAwait(false);
        if (StaleToken(answer.Code, answer.Body))
        {
            answer = await Send(method, url, after, content, cancellationToken).ConfigureAwait(false);
        }

        if (after > 0)
        {
            _ = Interlocked.CompareExchange(ref _after, 0, after);   // a newer command's sequence stays
        }

        return answer;
    }

    // One call: the request with the token as it is now, and the answer's status and body.
    private async Task<(HttpStatusCode Code, byte[] Body)> Send(HttpMethod method, Uri url, long after, byte[]? content, CancellationToken cancellationToken)
    {
        using var request = await Request(method, url, after, cancellationToken).ConfigureAwait(false);
        if (content is not null)
        {
            // A command's JSON body (CreateItem, CloseItem); a retry after a stale token sends it again, unchanged.
            request.Content = new ByteArrayContent(content) { Headers = { ContentType = new("application/json") } };
        }

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return (response.StatusCode, await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
    }

    // SND-AUTH-001 (API §6: a human call without this start's token): a restarted host has minted a new token, so the
    // call is retried exactly once with the token read again from auth/human.token (orchestrator decision, PR #184). A
    // second refusal is the answer. The check keys off the code, never the detail sentence.
    private static bool StaleToken(HttpStatusCode code, byte[] body) =>
        string.Equals(Refusal(code, body)?.Code, TokenRefused, StringComparison.Ordinal);

    // A refusal Sindri explains is Sindri's code and its detail as they are (API §3.6), so the window shows Sindri's own
    // sentence and fix. Built like the CLI's reading of a problem (ControlPlaneClient): the code is Sindri's catalogue's,
    // not the app's. A 403 (API §6: SND-AUTH-001 for a missing or stale token) needs only a title; a command's refusal
    // (409 SND-API-010 and the like, UI spec §4.1 Error) carries an SND title and its detail. Anything else that isn't a
    // success stays SND-UI-003.
    internal static Error? Refusal(HttpStatusCode status, byte[] body)
    {
        if (status is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices)
        {
            return null;
        }

        var problem = AwJson.Deserialize<ProblemView>(body, Json);
        if (problem is not { IsSuccess: true, Value.Title: { Length: > 0 } code })
        {
            return null;
        }

        var detail = problem.Value.Detail;
        var error = status == HttpStatusCode.Forbidden
            ? new Error(code, ErrorKind.Forbidden, detail is { Length: > 0 } ? detail : code)
            : code.StartsWith("SND-", StringComparison.Ordinal) && detail is { Length: > 0 } ? new Error(code, KindOf(status), detail) : null;
        return error is null ? null : ContextRefusal.Extend(error, body);
    }

    private static ErrorKind KindOf(HttpStatusCode status) => (int)status switch
    {
        400 => ErrorKind.Invalid,
        404 => ErrorKind.NotFound,
        409 => ErrorKind.Conflict,
        429 => ErrorKind.RateLimited,
        504 => ErrorKind.Timeout,
        _ => ErrorKind.Unavailable,
    };

    // Every request says who the app is to the API (UI spec §9.1, API §6): a human, from the UI, with this start's token
    // (read at each call, like the CLI's, so a restarted control plane's new one is sent with no app restart). Never
    // X-Sindri-Terminal (the app is no terminal, so protected actions stay at the CLI) and never Origin (commands refuse
    // it). With no token, or one the platform can't read, the call goes without it and Sindri's SND-AUTH-001 says why.
    // The token goes only into this header: never into an error, the log or the window. The view queries after an
    // accepted command carries its sequence as X-Sindri-After; the event stream, pings, the catalog and commands never do.
    private async ValueTask<HttpRequestMessage> Request(HttpMethod method, Uri url, long after, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Sindri-Caller", "human");
        request.Headers.Add("X-Sindri-Client", "ui");
        if (await HumanToken(cancellationToken).ConfigureAwait(false) is { } value)
        {
            request.Headers.Add("X-Sindri-Token", value);
        }

        if (after > 0)
        {
            request.Headers.Add("X-Sindri-After", after.ToString(CultureInfo.InvariantCulture));
        }

        return request;
    }

    private async ValueTask<string?> HumanToken(CancellationToken cancellationToken)
    {
        try
        {
            return await token.Read(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SindriApiLog.TokenUnreadable(_log, token.Location, e.GetType().Name);
            return null;
        }
    }
}
