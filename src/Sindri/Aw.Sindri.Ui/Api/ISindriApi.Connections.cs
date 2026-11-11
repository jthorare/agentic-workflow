using Aw.Kernel;
using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.Api;

/// <summary>The connection calls (<see cref="ISindriApi"/>, continued). No route refuses an agent session; each write is sent once.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/connections.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The connections, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ConnectionList>> ListConnections(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/connections/{name}.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The connection.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The connection (wrapped as served), SND-CONN-001, or SND-UI-002/003.</returns>
    public Task<Result<ConnectionOne>> GetConnection(Uri api, string name, CancellationToken cancellationToken);

    /// <summary>POST /sindri/connections: adds a connection. An oauth one answers with its consent URL.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="body">The connection to add; its secrets are sent once.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The connection and consent URL, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ConnectionAdded>> AddConnection(Uri api, AddConnectionBody body, CancellationToken cancellationToken);

    /// <summary>POST /sindri/connections/{name}/test.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The connection.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The test's outcome, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ConnectionTested>> TestConnection(Uri api, string name, CancellationToken cancellationToken);

    /// <summary>POST /sindri/connections/{name}/signin: starts the browser sign-in again.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The connection.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The connection and consent URL, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ConnectionAdded>> SignInConnection(Uri api, string name, CancellationToken cancellationToken);

    /// <summary>POST /sindri/connections/{name}/rename: changes the label, keeps the secret.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The connection.</param>
    /// <param name="body">The new label.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new and the old name, the API's refusal (SND-CONN-001, -008, -010 or SND-CLI-003), or SND-UI-002/003.</returns>
    public Task<Result<ConnectionRenamed>> RenameConnection(Uri api, string name, RenameConnectionBody body, CancellationToken cancellationToken);

    /// <summary>DELETE /sindri/connections/{name}.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The connection.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The removal, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ConnectionRemoved>> RemoveConnection(Uri api, string name, CancellationToken cancellationToken);
}
