using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The profile route (<see cref="ISindriApi"/>, continued): the plain-words summary.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/profile: the profile's plain-words approval summary (null while there is nothing to approve).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The read, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ProfileRead>> ProfileSummary(Uri api, CancellationToken cancellationToken);
}
