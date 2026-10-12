using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>Pings the recorded endpoint over loopback HTTP (no proxy), with a 2 s budget.</summary>
internal sealed class HttpControlPlaneProbe(IEndpointFile endpoints, JsonSerializerOptions options) : IControlPlaneProbe, IDisposable
{
    /// <summary>How long a client waits for the control plane.</summary>
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    private readonly HttpClient _http = new(new SocketsHttpHandler { UseProxy = false, ConnectTimeout = Budget }) { Timeout = Budget };

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
            return SindriErrors.ApiTimeout.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{endpoint.Url.Authority} didn't answer within {Budget.TotalSeconds:0} s"));
        }
    }
}
