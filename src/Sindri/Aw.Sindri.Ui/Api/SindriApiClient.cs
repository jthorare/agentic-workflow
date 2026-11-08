using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Ui.Api;

/// <summary>
/// The typed client of Sindri's loopback HTTP API (docs/csharp/sindri.md "Command layer and API"). A call that doesn't
/// answer within 120 s is unreachable (a context read of a large map takes seconds); the event stream has no timeout and
/// ends with its connection.
/// </summary>
/// <param name="http">The HTTP client, with no timeout of its own (the app's, or one over a canned handler in tests).</param>
/// <param name="token">The control plane's per-start human token, read at each call (API spec §6).</param>
/// <param name="clock">The clock the 120 s deadline runs on (the app's, so tests drive it with a fake one).</param>
/// <param name="log">The app's log, where an unreadable body's parser detail goes (never the window); none by default.</param>
internal sealed partial class SindriApiClient(HttpClient http, IHumanTokenSource token, TimeProvider clock, ILogger? log = null) : ISindriApi
{
    private readonly ILogger _log = log ?? NullLogger.Instance;

    /// <summary>
    /// The version of Sindri's API this build speaks (API spec "Versioning": the status reports apiVersion from P1). A
    /// status that reports another one is the Version mismatch state (UI spec §4.1); one that reports none (S1) is not.
    /// </summary>
    internal const int AppApiVersion = 1;

    // Every call's deadline: long enough for a context read of a large map (seconds), and well past the server's 2 s
    // wait on a view query carrying X-Sindri-After.
    internal static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(120);

    /// <summary>The app's JSON options (the AwJson profile over the app's context).</summary>
    internal static JsonSerializerOptions Json { get; } = AwJson.CreateOptions(SindriUiJsonContext.Default);

    public Task<Result<PingView>> Ping(Uri api, CancellationToken cancellationToken) => Get<PingView>(api, "/sindri/ping", cancellationToken);

    public Task<Result<StatusView>> Status(Uri api, CancellationToken cancellationToken) => Get<StatusView>(api, "/sindri/status", cancellationToken, view: true);

    public Task<Result<CommandList>> Commands(Uri api, CancellationToken cancellationToken) => Get<CommandList>(api, "/sindri/commands", cancellationToken);

    public Task<Result<ServiceList>> Services(Uri api, CancellationToken cancellationToken) => Get<ServiceList>(api, "/sindri/services", cancellationToken, view: true);

    public Task<Result<CommandAcceptedView>> RebuildViews(Uri api, CancellationToken cancellationToken) =>
        Call<CommandAcceptedView>(api, HttpMethod.Post, "/sindri/views/rebuild", cancellationToken);

    // LANE ANCHOR U3': a U3' call that fits this file goes here; the Needs you, decision and request calls are in SindriApiClient.NeedsYou.cs (this file is near its cap) and their routes in NeedsYouRoutes, like WorkflowRoutes. Insert immediately above this marker, never between the markers.
    // LANE ANCHOR U8: the U8 public sends; the workflow reads already have their routes in WorkflowRoutes. Insert immediately below this marker, never between the markers.

    public async IAsyncEnumerable<EventView> Events(Uri api, long after, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"/sindri/events?after={after}");
        await foreach (var item in Frames(new Uri(api, path), cancellationToken).ConfigureAwait(false))
        {
            if (item is { } frame && AwJson.Deserialize<EventView>(System.Text.Encoding.UTF8.GetBytes(frame.Data), Json) is { IsSuccess: true } e)
            {
                yield return e.Value;
            }
        }
    }

    // One event stream: null once the response is open (nothing has been sent yet), then each frame. Ends when the connection
    // does, or at once when it didn't answer 2xx.
    private async IAsyncEnumerable<SseItem<string>?> Frames(Uri url, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var response = await Open(url, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            yield break;
        }

        var stream = await Body(response, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            yield break;
        }

        await using (stream.ConfigureAwait(false))
        {
            yield return null;
            var items = SseParser.Create(stream).EnumerateAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
            await using (items.ConfigureAwait(false))
            {
                while (await Next(items).ConfigureAwait(false))
                {
                    yield return items.Current;
                }
            }
        }
    }

    // The open event-stream response (owned by the caller), or null when it didn't answer 2xx or nothing answered.
    private async Task<HttpResponseMessage?> Open(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                using var request = await Request(HttpMethod.Get, url, after: 0, cancellationToken).ConfigureAwait(false);
                request.Headers.Accept.ParseAdd("text/event-stream");
                var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                // A stale token is retried once with the token read again, as Get's calls are; anything else ends the stream.
                using (response)
                {
                    if (attempt > 1 || !StaleToken(response.StatusCode, await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)))
                    {
                        return null;
                    }
                }
            }
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<Stream?> Body(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    // A connection that breaks mid-stream ends the stream, like a clean close: the caller reconnects either way.
    private static async ValueTask<bool> Next(IAsyncEnumerator<SseItem<string>> items)
    {
        try
        {
            return await items.MoveNextAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
