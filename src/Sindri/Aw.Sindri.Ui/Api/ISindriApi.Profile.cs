using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The profile's approval routes (<see cref="ISindriApi"/>, continued): the plain-words summary and the approval.</summary>
internal partial interface ISindriApi
{
    /// <summary>POST /sindri/profile/approve: approves the live profile. A human token only; sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The full hash.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The approval, SND-AUTH-002 for an agent session, the API's other refusals (a stale hash, a wrong answer), or SND-UI-002/003.</returns>
    public Task<Result<ProfileApproved>> ApproveProfile(Uri api, ApproveProfileRequest request, CancellationToken cancellationToken);

    /// <summary>GET /sindri/profile: the profile's plain-words approval summary (null while there is nothing to approve).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The read, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ProfileRead>> ProfileSummary(Uri api, CancellationToken cancellationToken);
}
