using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The CLI's typed client of the control plane's API (factory addendum §17.27.1: the CLI is a client of the same
/// handlers the UI calls). It finds the process through the endpoint record and the probe, so a stale record reads as
/// 'not running', never as a hang.
/// </summary>
internal sealed class ControlPlaneClient(IControlPlaneProbe probe, JsonSerializerOptions options, TimeSpan? timeout = null) : IDisposable
{
    private readonly HttpClient _http = new(new SocketsHttpHandler { UseProxy = false }) { Timeout = timeout ?? TimeSpan.FromSeconds(10) };

    public void Dispose() => _http.Dispose();

    /// <summary>GET a route and read its answer as <typeparamref name="T"/>.</summary>
    /// <param name="route">The route under the base URL, e.g. '/sindri/status'.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="T">The response record (in the package's JSON context).</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<T>> Get<T>(string route, CancellationToken ct) => Send<T>(HttpMethod.Get, route, content: null, ct);

    /// <summary>POST <paramref name="body"/> (or nothing) to a route and read its answer.</summary>
    /// <param name="route">The route.</param>
    /// <param name="body">The request record, or null.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="TBody">The request type.</typeparam>
    /// <typeparam name="T">The response type.</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<T>> Post<TBody, T>(string route, TBody? body, CancellationToken ct)
    {
        var content = body is null ? null : new ByteArrayContent(AwJson.Serialize(body, options)) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };
        return Send<T>(HttpMethod.Post, route, content, ct);
    }

    private async Task<Result<T>> Send<T>(HttpMethod method, string route, HttpContent? content, CancellationToken ct)
    {
        var found = await probe.Check(ct).ConfigureAwait(false);
        if (found.IsError)
        {
            content?.Dispose();
            return found.Error;
        }

        using var request = new HttpRequestMessage(method, new Uri(found.Value.Endpoint.Url, route)) { Content = content };
        try
        {
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? AwJson.Deserialize<T>(bytes, options) : Problem(bytes, (int)response.StatusCode);
        }
        catch (HttpRequestException e)
        {
            return SindriErrors.NotRunning.With($"the control plane went away: {e.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return SindriErrors.ApiTimeout.With(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{found.Value.Endpoint.Url.Authority} didn't answer {route} within {_http.Timeout.TotalSeconds:0} s"));
        }
    }

    private Error Problem(byte[] bytes, int status)
    {
        var problem = AwJson.Deserialize<ApiProblem>(bytes, options);
        return problem.IsSuccess && problem.Value.Title is { } code
            ? new Error(code, status >= 500 ? ErrorKind.Unavailable : ErrorKind.Invalid, problem.Value.Detail ?? code)
            : SindriErrors.ApiNoCode.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the API answered HTTP {status} without an error code"));
    }
}
