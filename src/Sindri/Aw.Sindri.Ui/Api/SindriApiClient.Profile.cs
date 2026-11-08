using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>The profile's approval routes (<see cref="SindriApiClient"/>, continued). The approval is sent once.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ProfileApproved>> ApproveProfile(Uri api, ApproveProfileRequest request, CancellationToken cancellationToken) =>
        Call<ProfileApproved>(api, HttpMethod.Post, "/sindri/profile/approve", cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<ProfileRead>> ProfileSummary(Uri api, CancellationToken cancellationToken) => Get<ProfileRead>(api, "/sindri/profile", cancellationToken);
}
