using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.Api;

/// <summary>The connection calls (<see cref="SindriApiClient"/>, continued). Each write is sent once.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ConnectionList>> ListConnections(Uri api, CancellationToken cancellationToken) =>
        Get<ConnectionList>(api, ConnectionRoutes.Root, cancellationToken);

    /// <inheritdoc />
    public Task<Result<ConnectionOne>> GetConnection(Uri api, string name, CancellationToken cancellationToken) =>
        Get<ConnectionOne>(api, ConnectionRoutes.One(name), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ConnectionAdded>> AddConnection(Uri api, AddConnectionBody body, CancellationToken cancellationToken) =>
        Call<ConnectionAdded>(api, HttpMethod.Post, ConnectionRoutes.Root, cancellationToken, new CallOptions(Content: AwJson.Serialize(body, Json)));

    /// <inheritdoc />
    public Task<Result<ConnectionTested>> TestConnection(Uri api, string name, CancellationToken cancellationToken) =>
        Call<ConnectionTested>(api, HttpMethod.Post, ConnectionRoutes.Test(name), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ConnectionAdded>> SignInConnection(Uri api, string name, CancellationToken cancellationToken) =>
        Call<ConnectionAdded>(api, HttpMethod.Post, ConnectionRoutes.SignIn(name), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ConnectionRemoved>> RemoveConnection(Uri api, string name, CancellationToken cancellationToken) =>
        Call<ConnectionRemoved>(api, HttpMethod.Delete, ConnectionRoutes.One(name), cancellationToken);
}
