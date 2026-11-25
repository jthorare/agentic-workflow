using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// Pings the recorded endpoint over loopback HTTP (no proxy). The connect is bounded by <see cref="Connect"/>, so a dead
/// port fails fast; the answer gets <see cref="Answer"/>, so a cold control plane's first ping on a loaded box still
/// completes (#2641) while one that accepts and never answers is still reported.
/// </summary>
/// <param name="endpoints">Reads the endpoint record.</param>
/// <param name="options">The JSON options for the ping.</param>
/// <param name="answer">The cap on the whole ping call; null means <see cref="Answer"/>.</param>
internal sealed class HttpControlPlaneProbe(IEndpointFile endpoints, JsonSerializerOptions options, TimeSpan? answer = null) : IControlPlaneProbe, IDisposable
{
    /// <summary>How long the probe waits to connect to the recorded port.</summary>
    internal static readonly TimeSpan Connect = TimeSpan.FromSeconds(2);

    /// <summary>How long the probe waits for the whole ping, answer included: well above a cold first request on a loaded box.</summary>
    internal static readonly TimeSpan Answer = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http = new(new SocketsHttpHandler { UseProxy = false, ConnectTimeout = Connect }) { Timeout = answer ?? Answer };

    public void Dispose() => _http.Dispose();

    public async ValueTask<Result<(ControlEndpoint Endpoint, Ping Ping)>> Check(CancellationToken cancellationToken)
    {
        var read = await endpoints.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } endpoint)
        {
            return SindriErrors.NotRunning.With("no control plane is recorded");
        }

        try
        {
            using var response = await _http.GetAsync(new Uri(endpoint.Url, "/sindri/ping"), cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var ping = response.IsSuccessStatusCode ? AwJson.Deserialize<Ping>(body, options) : null;
            return ping is { IsSuccess: true } && ping.Value.Pid == endpoint.Pid && ping.Value.Epoch == endpoint.Epoch
                ? (endpoint, ping.Value)
                : SindriErrors.NotRunning.With($"{endpoint.Url.Authority} answered, but not as the recorded control plane");
        }
        catch (HttpRequestException)
        {
            return SindriErrors.NotRunning.With($"nothing answers at {endpoint.Url.Authority}; the record is stale");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.ApiTimeout.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{endpoint.Url.Authority} didn't answer the ping within {_http.Timeout.TotalSeconds:0} s"));
        }
    }
}
