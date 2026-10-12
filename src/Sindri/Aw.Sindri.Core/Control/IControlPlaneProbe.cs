using Aw.Kernel;

namespace Aw.Sindri.Core.Control;

/// <summary>Whether the control plane answers (port): reads the endpoint record and pings it.</summary>
public interface IControlPlaneProbe
{
    /// <summary>The recorded endpoint and its answer.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The endpoint and ping, or SND-API-001 (no record, or nothing answers) or SND-API-002 (no answer in time).</returns>
    public ValueTask<Result<(ControlEndpoint Endpoint, Ping Ping)>> Check(CancellationToken cancellationToken);
}
