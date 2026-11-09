using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Sends an 'http' node's request (agent-first PR 9): only over https to a host of 'workflow.http.hosts', never to a loopback address, never with a credential header, without following a
/// redirect to another host, and reading at most 1 MB of the answer. Before connections exist (PR 10) a request is always unauthenticated. The same pattern as the Slack client's host policy.
/// </summary>
/// <param name="handler">What sends the request; a fake in tests. It must not follow redirects itself.</param>
/// <param name="hosts">Reads the allowlist each time, so a profile edit applies at once.</param>
/// <param name="budget">How long one request may take; 30 s when null. A test passes a short one.</param>
internal sealed class GraphHttpClient(HttpMessageHandler handler, Func<CancellationToken, ValueTask<IReadOnlyList<string>>> hosts, TimeSpan? budget = null) : IGraphHttpPort
{
    /// <summary>The most bytes of an answer that are read.</summary>
    internal const int MaxBodyBytes = 1024 * 1024;

    private const int MaxRedirects = 3;

    private static readonly string[] _credentialHeaders = ["authorization", "proxy-authorization", "cookie", "x-api-key", "api-key"];

    private static readonly string[] _credentialWords = ["token", "secret", "password"];

    /// <summary>The allowlist of the approved profile; none (so every request is refused) when the profile can't be read.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The hosts.</returns>
    internal static async ValueTask<IReadOnlyList<string>> HostsOf(IApprovedProfile profile, CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsSuccess ? latest.Value?.Profile.Workflow?.Http?.Hosts ?? [] : [];
    }

    public async ValueTask<Result<GraphHttpResponse>> Send(GraphHttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Refusal(request) is { } refused)
        {
            return SindriErrors.GraphHttpRefused.With(refused);
        }

        var allowed = await hosts(cancellationToken).ConfigureAwait(false);
        if (!allowed.Contains(request.Url.Host, StringComparer.OrdinalIgnoreCase))
        {
            return SindriErrors.GraphHttpRefused.With($"'{request.Url.Host}' is not on workflow.http.hosts (fix: add it to the profile's workflow.http.hosts)");
        }

        try
        {
            // why: sends use ResponseHeadersRead, so HttpClient.Timeout stops at the headers; one linked budget over the sends AND the body read keeps a trickling body from holding the node for the activity's hour
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(budget ?? TimeSpan.FromSeconds(30));
            using var client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
            return await Follow(client, request, timeout.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException e)   // boundary: the network or the peer failed
        {
            return SindriErrors.GraphHttpFailed.With($"the request to {request.Url.Host} failed: {e.Message}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)   // boundary: the request budget ran out (headers or body), not a cancel
        {
            return SindriErrors.GraphHttpFailed.With($"the request to {request.Url.Host} timed out after {(budget ?? TimeSpan.FromSeconds(30)).TotalSeconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} s (sending and reading the answer)");
        }
    }

    /// <summary>Why <paramref name="request"/> may not be sent at all, or null: not https, user info, a credential header, or an address that is the box itself.</summary>
    private static string? Refusal(GraphHttpRequest request)
    {
        var url = request.Url;
        if (url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length > 0)
        {
            return "only https addresses without a user name or password are sent (fix: use https://host/path)";
        }

        if (request.Headers.Keys.FirstOrDefault(IsCredentialHeader) is { } header)
        {
            return $"the header '{header}' carries a credential, and http nodes send none until connections exist (fix: remove it)";
        }

        return IsLoopback(url) ? $"'{url.Host}' is this machine, which http nodes never reach" : null;
    }

    private static bool IsCredentialHeader(string name) =>
        _credentialHeaders.Contains(name, StringComparer.OrdinalIgnoreCase) || _credentialWords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));

    // why: an IP address is never on the allowlist (the profile takes names only), so a literal one is a way round it: refused, loopback or not
    private static bool IsLoopback(Uri url) =>
        url.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
        || string.Equals(url.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || url.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static async ValueTask<Result<GraphHttpResponse>> Follow(HttpClient client, GraphHttpRequest request, CancellationToken cancellationToken)
    {
        var (method, url, body) = (new HttpMethod(request.Method), request.Url, request.Body);
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            using var message = Message(method, url, request.Headers, body);
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!IsRedirect(response.StatusCode) || response.Headers.Location is not { } location)
            {
                return await Read(response, cancellationToken).ConfigureAwait(false);
            }

            var next = new Uri(url, location);
            if (!string.Equals(next.Host, url.Host, StringComparison.OrdinalIgnoreCase) || next.Scheme != Uri.UriSchemeHttps)
            {
                return SindriErrors.GraphHttpRefused.With($"the answer redirects to {next.Scheme}://{next.Host}, another host than the one asked; http nodes never follow that (fix: send the request to {next.Host} and list it in workflow.http.hosts)");
            }

            url = next;
            if (response.StatusCode is not (HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
            {
                (method, body) = (HttpMethod.Get, null);   // why: 301, 302 and 303 turn a request into a plain GET, as a browser does
            }
        }

        return SindriErrors.GraphHttpFailed.With($"the answer redirected more than {MaxRedirects} times");
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static HttpRequestMessage Message(HttpMethod method, Uri url, IReadOnlyDictionary<string, string> headers, string? body)
    {
        var message = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            message.Content = new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }

        foreach (var (name, value) in headers)
        {
            // why: a header such as Content-Type belongs to the content, the rest to the message
            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                _ = message.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return message;
    }

    private static async ValueTask<Result<GraphHttpResponse>> Read(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                if (buffer.Length > MaxBodyBytes)
                {
                    return SindriErrors.GraphHttpFailed.With("the answer is over 1 MB, which an http node does not read (fix: ask for less, for example with a query that narrows it)");
                }
            }

            return new GraphHttpResponse((int)response.StatusCode, Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length));
        }
    }
}
