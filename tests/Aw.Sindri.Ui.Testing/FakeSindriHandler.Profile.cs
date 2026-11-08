using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The profile routes of <see cref="FakeSindriHandler"/>: the summary and the approval (setup-once spec §8).</summary>
internal sealed partial class FakeSindriHandler
{
    private const string ProfilePath = "/sindri/profile";
    private const string ProfileApprovePath = "/sindri/profile/approve";

    private static bool IsProfile(HttpRequestMessage request, string path) =>
        (string.Equals(path, ProfilePath, StringComparison.Ordinal) && request.Method == HttpMethod.Get) || (string.Equals(path, ProfileApprovePath, StringComparison.Ordinal) && request.Method == HttpMethod.Post);

    private async Task<HttpResponseMessage> ProfileRoute(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        return string.Equals(path, ProfilePath, StringComparison.Ordinal)
            ? Answer(Counted(await server.ProfileSummary(uri, cancellationToken), server.ProfileAnswer))
            : Answer(Counted(await server.ApproveProfile(uri, await Body<ApproveProfileRequest>(request, cancellationToken), cancellationToken), server.ApproveProfileAnswer));
    }
}
