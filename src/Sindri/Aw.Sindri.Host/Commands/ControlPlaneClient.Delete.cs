using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

internal sealed partial class ControlPlaneClient
{
    /// <summary>Sends a DELETE (memory forget): the route's query string carries the selector.</summary>
    /// <param name="route">The route under /sindri, with its query.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <typeparam name="T">The answer's type.</typeparam>
    /// <returns>The answer or the error.</returns>
    public Task<Result<ApiAnswer<T>>> Delete<T>(string route, CancellationToken ct) => Send<T>(HttpMethod.Delete, route, content: null, after: null, ct);
}
