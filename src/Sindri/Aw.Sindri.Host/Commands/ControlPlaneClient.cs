using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The CLI's typed client of the control plane's API (factory addendum §17.27.1: the CLI is a client of the same
/// handlers the UI calls). It finds the process through the endpoint record and the probe, so a stale record reads as
/// 'not running', never as a hang. Every call carries the caller headers (API spec §6); an answer keeps its raw body.
/// </summary>
/// <param name="probe">Finds the running control plane.</param>
/// <param name="options">The package's AwJson options.</param>
/// <param name="headers">The caller headers; none (read as a session) when null.</param>
/// <param name="timeout">A hard cap on one call (AW_SINDRI_CLI_TIMEOUT); none by default, so a slow but healthy answer completes. Connecting is bounded by <see cref="CliTimeout.Connect"/> instead.</param>
/// <param name="refusal">A refusal every call answers with instead of calling (a malformed AW_SINDRI_CLI_TIMEOUT), so verbs that never call the control plane aren't affected by it.</param>
/// <param name="handler">The HTTP handler (tests); a loopback handler without a proxy when null.</param>
internal sealed class ControlPlaneClient(IControlPlaneProbe probe, JsonSerializerOptions options, CallerHeaders? headers = null, TimeSpan? timeout = null,
    HttpMessageHandler? handler = null, Error? refusal = null) : IDisposable
{
    private readonly HttpClient _http = new(handler ?? new SocketsHttpHandler { UseProxy = false, ConnectTimeout = CliTimeout.Connect }) { Timeout = timeout ?? Timeout.InfiniteTimeSpan };
    private readonly CallerHeaders _headers = headers ?? CallerHeaders.None;

    /// <summary>Why this CLI calls as a session ('the per-start human token isn't readable'), or null when it calls as a human.</summary>
    public string? SessionReason => _headers.SessionReason;

    /// <summary>Reads the per-start token now and answers <see cref="SessionReason"/>: a human's call carries it, a session's doesn't.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Why this CLI calls as a session, or null when it calls as a human.</returns>
    public ValueTask<string?> ResolveSessionReason(CancellationToken cancellationToken) => _headers.Resolve(cancellationToken);

    /// <summary>The cap on one call, <see cref="Timeout.InfiniteTimeSpan"/> when none is set (tests: a call's real timeout can't be waited out).</summary>
    internal TimeSpan CallTimeout => _http.Timeout;

    /// <summary>The refusal every call answers with (a malformed AW_SINDRI_CLI_TIMEOUT), or null; a start checks it before it launches the control plane.</summary>
    internal Error? SettingRefusal { get; } = refusal;

    public void Dispose() => _http.Dispose();

    /// <summary>GET a route and read its answer as <typeparamref name="T"/>.</summary>
    /// <param name="route">The route under the base URL, e.g. '/sindri/status'.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="T">The response record (in the package's JSON context).</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<ApiAnswer<T>>> Get<T>(string route, CancellationToken ct) => Send<T>(HttpMethod.Get, route, content: null, after: null, ct);

    /// <summary>GET a route so it reflects every write up to <paramref name="after"/> (X-Sindri-After, API spec §2.4).</summary>
    /// <param name="route">The route under the base URL.</param>
    /// <param name="after">The ledger sequence the read must reflect.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="T">The response record (in the package's JSON context).</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<ApiAnswer<T>>> GetAfter<T>(string route, long after, CancellationToken ct) => Send<T>(HttpMethod.Get, route, content: null, after, ct);

    /// <summary>
    /// GET an event stream route (API spec §5) and read its frames as they arrive: the frames' id, event and data, with the ': ping' comments dropped.
    /// A refusal before the stream opens (SND-API-012 for a 429, not running) arrives as one error, then the sequence ends; so does the sequence
    /// when the server closes the stream or the connection breaks. No call timeout applies once the headers are in. Cancelling <paramref name="ct"/> ends it with the cancellation.
    /// </summary>
    /// <param name="route">The route under the base URL, e.g. '/sindri/events?after=12'.</param>
    /// <param name="ct">Ends the stream.</param>
    /// <returns>The frames, or the refusal.</returns>
    public async IAsyncEnumerable<Result<StreamFrame>> Stream(string route, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var opened = await Open(route, ct).ConfigureAwait(false);
        if (opened.IsError)
        {
            yield return opened.Error;
            yield break;
        }

        using var response = opened.Value;
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false));
        await foreach (var frame in SseFrames.Read(reader, ct).ConfigureAwait(false))
        {
            yield return frame;
        }
    }

    /// <summary>
    /// GET a view route so it reflects every write made so far (API spec §2.4): reads the ledger head from GET /sindri/status, then sends it as
    /// X-Sindri-After, so a read right after a write never lags the projector. Views still behind after 2 s are the server's SND-API-011.
    /// When the status says the views stall or rebuild, no header is sent, so the answer arrives with its asOf and sentence instead of that error.
    /// </summary>
    /// <param name="route">The route under the base URL, e.g. '/sindri/items'.</param>
    /// <param name="ct">Cancels the calls.</param>
    /// <typeparam name="T">The response record (in the package's JSON context).</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public async Task<Result<ApiAnswer<T>>> GetView<T>(string route, CancellationToken ct)
    {
        var status = await Send<GetStatusResponse>(HttpMethod.Get, "/sindri/status", content: null, after: null, ct).ConfigureAwait(false);
        if (status.IsError)
        {
            return status.Error;
        }

        // Views that stall or rebuild can't catch up to the head, so the header would only turn the answer into SND-API-011: the answer's asOf
        // already says its rows may be partial, and the verb exits 1 on it.
        var views = status.Value.Value.Views;
        var head = views is { Stalled: false, Rebuilding: false } ? views.LedgerHead : (long?)null;
        return await Send<T>(HttpMethod.Get, route, content: null, head, ct).ConfigureAwait(false);
    }

    /// <summary>POST <paramref name="body"/> (or nothing) to a route and read its answer.</summary>
    /// <param name="route">The route.</param>
    /// <param name="body">The request record, or null.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="TBody">The request type.</typeparam>
    /// <typeparam name="T">The response type.</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<ApiAnswer<T>>> Post<TBody, T>(string route, TBody? body, CancellationToken ct)
    {
#pragma warning disable CA2000 // Send disposes the content, on its failure paths and with the request
        var content = body is null ? null : new ByteArrayContent(AwJson.Serialize(body, options)) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };
#pragma warning restore CA2000
        return Send<T>(HttpMethod.Post, route, content, after: null, ct);
    }

    /// <summary>QUERY <paramref name="body"/> to a route and read its answer: the HTTP method for a read whose question is too large for a URL (an IETF draft; safe and idempotent, never a command).</summary>
    /// <param name="route">The route.</param>
    /// <param name="body">The request record.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="TBody">The request type.</typeparam>
    /// <typeparam name="T">The response type.</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<ApiAnswer<T>>> Query<TBody, T>(string route, TBody body, CancellationToken ct)
    {
#pragma warning disable CA2000 // Send disposes the content, on its failure paths and with the request
        var content = new ByteArrayContent(AwJson.Serialize(body, options)) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };
#pragma warning restore CA2000
        return Send<T>(HttpMethod.Query, route, content, after: null, ct);
    }

    /// <summary>The longest query string a GET carries; a longer one (Kestrel's request line is 8 KB) goes as a QUERY with the same fields as the JSON body.</summary>
    internal const int MaxGetQuery = 4000;

    /// <summary>GET <paramref name="route"/> with <paramref name="query"/>, or QUERY it with <paramref name="body"/> when the query is too long for a request line; one question, two transports.</summary>
    /// <param name="route">The route.</param>
    /// <param name="query">The query string, with its leading '?'.</param>
    /// <param name="body">The same question as the request record.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="TBody">The request type.</typeparam>
    /// <typeparam name="T">The response type.</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<ApiAnswer<T>>> GetOrQuery<TBody, T>(string route, string query, TBody body, CancellationToken ct) =>
        query.Length > MaxGetQuery ? Query<TBody, T>(route, body, ct) : Get<T>(route + query, ct);

    /// <summary>POST <paramref name="body"/> to a route a launch's own proof authorizes (API spec §8.1): sent as the X-Sindri-Launch-Secret header, with the caller headers.</summary>
    /// <param name="route">The route.</param>
    /// <param name="body">The request record.</param>
    /// <param name="proof">The launch's proof.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="TBody">The request type.</typeparam>
    /// <typeparam name="T">The response type.</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<ApiAnswer<T>>> PostProven<TBody, T>(string route, TBody body, string proof, CancellationToken ct)
    {
#pragma warning disable CA2000 // Send disposes the content, on its failure paths and with the request
        var content = new ByteArrayContent(AwJson.Serialize(body, options)) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };
#pragma warning restore CA2000
        content.Headers.Add(SindriHeaders.LaunchSecret, proof);   // sent with the request
        return Send<T>(HttpMethod.Post, route, content, after: null, ct);
    }

    /// <summary>GET a route a launch's own proof authorizes (API spec §8.1): sent as the X-Sindri-Launch-Secret header, with the caller headers.</summary>
    /// <param name="route">The route.</param>
    /// <param name="proof">The launch secret.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="T">The response type.</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<ApiAnswer<T>>> GetProven<T>(string route, string proof, CancellationToken ct)
    {
#pragma warning disable CA2000 // Send disposes the content, on its failure paths and with the request
        var content = new ByteArrayContent([]);
#pragma warning restore CA2000
        content.Headers.Add(SindriHeaders.LaunchSecret, proof);   // an empty body only carries the proof, as PostProven's does
        return Send<T>(HttpMethod.Get, route, content, after: null, ct);
    }

    private async Task<Result<ApiAnswer<T>>> Send<T>(HttpMethod method, string route, HttpContent? content, long? after, CancellationToken ct)
    {
        if (SettingRefusal is { } refused)
        {
            content?.Dispose();
            return refused;
        }

        var found = await probe.Check(ct).ConfigureAwait(false);
        if (found.IsError)
        {
            content?.Dispose();
            return found.Error;
        }

        using var request = new HttpRequestMessage(method, new Uri(found.Value.Endpoint.Url, route)) { Content = content };
        await _headers.Apply(request, ct).ConfigureAwait(false);
        if (after is { } sequence)
        {
            request.Headers.Add(SindriHeaders.After, sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        try
        {
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Problem(bytes, (int)response.StatusCode);
            }

            var value = AwJson.Deserialize<T>(bytes, options);
            return value.IsError ? value.Error : new ApiAnswer<T>(value.Value, bytes);
        }
        catch (Exception e) when (Transport(e, ct))
        {
            return Failed(e, found.Value.Endpoint.Url.Authority, $"answer {route}");
        }
    }

    /// <summary>A failure of the call itself: the connection or the cap, not a Ctrl-C (which propagates).</summary>
    private static bool Transport(Exception e, CancellationToken ct) => e is HttpRequestException || (e is TaskCanceledException && !ct.IsCancellationRequested);

    /// <summary>A refused or broken connection is 'went away'; a connect that timed out and a call over the cap are SND-API-002.</summary>
    private Error Failed(Exception e, string authority, string doing) => e switch
    {
        HttpRequestException { InnerException: TimeoutException } => ConnectTimeout(authority),
        HttpRequestException => SindriErrors.NotRunning.With($"the control plane went away: {e.Message}"),
        _ => SindriErrors.ApiTimeout.With(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{authority} didn't {doing} within {_http.Timeout.TotalSeconds:0} s ({CliTimeout.Variable})")),
    };

    private static Error ConnectTimeout(string authority) => SindriErrors.ApiTimeout.With(string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"couldn't connect to {authority} within {CliTimeout.Connect.TotalSeconds:0} s"));

    private async Task<Result<HttpResponseMessage>> Open(string route, CancellationToken ct)
    {
        if (SettingRefusal is { } refused)
        {
            return refused;
        }

        var found = await probe.Check(ct).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(found.Value.Endpoint.Url, route));
        await _headers.Apply(request, ct).ConfigureAwait(false);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        try
        {
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            using (response)
            {
                return Problem(await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false), (int)response.StatusCode);
            }
        }
        catch (Exception e) when (Transport(e, ct))
        {
            return Failed(e, found.Value.Endpoint.Url.Authority, $"open {route}");
        }
    }

    private Error Problem(byte[] bytes, int status)
    {
        var problem = AwJson.Deserialize<ApiProblem>(bytes, options);
        return problem.IsSuccess && problem.Value.Title is { } code
            ? new Error(code, Kind(status), Refusal(code, problem.Value.Detail ?? code) + RunningJob(code, problem.Value.Detail, problem.Value.RunningJob) + ProblemWords.Of(problem.Value))
            : SindriErrors.ApiNoCode.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the API answered HTTP {status} without an error code"));
    }

    private static ErrorKind Kind(int status) => status switch
    {
        >= 500 => ErrorKind.Unavailable,
        403 => ErrorKind.Forbidden,
        _ => ErrorKind.Invalid,
    };

    /// <summary>A refused start names the job that is already running and the command that shows it, unless the route's detail already says so.</summary>
    private static string RunningJob(string code, string? detail, string? job) =>
        code == SindriErrors.JobConflict.Code && !string.IsNullOrEmpty(job) && detail?.Contains("(fix:", StringComparison.Ordinal) != true ? $" (fix: sindri jobs show {SafeText.Line(SafeText.Escape(job), 60)})" : string.Empty;

    /// <summary>A refused action class names why this CLI called as a session (workflow spec §4.2), so the reader knows what to change.</summary>
    private string Refusal(string code, string detail) =>
        code == SindriErrors.AuthRefused.Code && _headers.SessionReason is { } reason ? $"{detail} (this CLI ran as a session: {reason})" : detail;
}
