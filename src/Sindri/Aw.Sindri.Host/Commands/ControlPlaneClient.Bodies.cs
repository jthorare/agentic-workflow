using System.Net.Http.Headers;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Host.Commands;

internal sealed partial class ControlPlaneClient
{
    /// <summary>PUT <paramref name="body"/> to a route and read its answer.</summary>
    /// <param name="route">The route.</param>
    /// <param name="body">The request record.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="TBody">The request type.</typeparam>
    /// <typeparam name="T">The response type.</typeparam>
    /// <returns>The answer, or the API's error.</returns>
    public Task<Result<ApiAnswer<T>>> Put<TBody, T>(string route, TBody body, CancellationToken ct)
    {
#pragma warning disable CA2000 // Send disposes the content, on its failure paths and with the request
        var content = new ByteArrayContent(AwJson.Serialize(body, options)) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };
#pragma warning restore CA2000
        return Send<T>(HttpMethod.Put, route, content, after: null, ct);
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
}
