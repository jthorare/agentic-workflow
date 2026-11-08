using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The takeover command (<see cref="ISindriApi"/>, continued).</summary>
internal partial interface ISindriApi
{
    /// <summary>POST /sindri/workflow/instances/{id}/commands: the takeover of an agent run. A human caller; sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="instanceId">The instance's id.</param>
    /// <param name="body">The command.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ApplyCommandAccepted>> ApplyCommand(Uri api, string instanceId, ApplyCommandBody body, CancellationToken cancellationToken);
}
