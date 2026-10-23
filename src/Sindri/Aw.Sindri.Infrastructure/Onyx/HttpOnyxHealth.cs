using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// IOnyxHealth over HTTP to the origin it is given: http://127.0.0.1:&lt;port&gt; for the managed stack, or the cluster address `onyx attach` has already checked against the environment: the health route, then the version route once health answers,
/// each with its own 3 s timeout. A refused connection, a timeout, a non-2xx or an answer that isn't the expected JSON is "not healthy" or a null version, never an exception.
/// </summary>
internal sealed class HttpOnyxHealth : IOnyxHealth
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(OnyxHealthJsonContext.Default);

    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;

    /// <summary>Initializes the adapter for the production timeout.</summary>
    /// <param name="client">The client.</param>
    public HttpOnyxHealth(HttpClient client)
        : this(client, TimeSpan.FromSeconds(3))
    {
    }

    /// <summary>Initializes the adapter with a per-request timeout (a test sets a short one).</summary>
    /// <param name="client">The client.</param>
    /// <param name="timeout">How long one request may take.</param>
    internal HttpOnyxHealth(HttpClient client, TimeSpan timeout)
    {
        (_client, _timeout) = (client, timeout);
    }

    /// <summary>Builds the client production uses: no redirect is followed and no proxy is used (the address is 127.0.0.1:port and nowhere else, whatever the environment's proxy settings or a server's Location say).</summary>
    /// <returns>The client.</returns>
#pragma warning disable CA2000 // the client owns the handler and disposes it
    internal static HttpClient CreateClient() => new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
#pragma warning restore CA2000

    /// <inheritdoc />
    public async ValueTask<OnyxProbe> Probe(OnyxOrigin origin, CancellationToken cancellationToken)
    {
        var baseUrl = origin.Url;
        if (await Get(new Uri(baseUrl, OnyxRoutes.Health), cancellationToken).ConfigureAwait(false) is null)
        {
            return new OnyxProbe(Healthy: false, BackendVersion: null);
        }

        var body = await Get(new Uri(baseUrl, OnyxRoutes.Version), cancellationToken).ConfigureAwait(false);
        var version = body is null ? null : AwJson.Deserialize<OnyxVersionWire>(System.Text.Encoding.UTF8.GetBytes(body), _json);
        return new OnyxProbe(Healthy: true, version is { IsSuccess: true } read ? read.Value.BackendVersion : null);
    }

    // The body of a 2xx answer, or null for anything else (no answer, a timeout, another status).
    private async Task<string?> Get(Uri uri, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        try
        {
            using var response = await _client.GetAsync(uri, deadline.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false) : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
