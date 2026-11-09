using Aw.Kernel;
using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.Api;

/// <summary>The connection calls (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ConnectionList>> ListConnections(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<ConnectionOne>> GetConnection(Uri api, string name, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<ConnectionAdded>> AddConnection(Uri api, AddConnectionBody body, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<ConnectionTested>> TestConnection(Uri api, string name, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<ConnectionAdded>> SignInConnection(Uri api, string name, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<ConnectionRemoved>> RemoveConnection(Uri api, string name, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
