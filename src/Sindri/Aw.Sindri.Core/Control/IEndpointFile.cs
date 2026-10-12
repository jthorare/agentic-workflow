using Aw.Kernel;

namespace Aw.Sindri.Core.Control;

/// <summary>The control plane's endpoint record (port): the server writes it once it listens; clients read it.</summary>
public interface IEndpointFile
{
    /// <summary>The recorded endpoint, or null when there is none.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The endpoint.</returns>
    public ValueTask<Result<ControlEndpoint?>> Read(CancellationToken cancellationToken);

    /// <summary>Records the endpoint (owner-only permissions).</summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Write(ControlEndpoint endpoint, CancellationToken cancellationToken);

    /// <summary>Removes the record if it still names <paramref name="pid"/>.</summary>
    /// <param name="pid">The process that wrote it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Remove(int pid, CancellationToken cancellationToken);
}
