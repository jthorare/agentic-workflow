namespace Aw.Sindri.Ui.Testing;

/// <summary>The profile routes of <see cref="FakeSindriHandler"/>: the summary (setup-once spec §8).</summary>
internal sealed partial class FakeSindriHandler
{
    private const string ProfilePath = "/sindri/profile";

    private static bool IsProfile(HttpRequestMessage request, string path) =>
        string.Equals(path, ProfilePath, StringComparison.Ordinal) && request.Method == HttpMethod.Get;

    private async Task<HttpResponseMessage> ProfileRoute(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        return Answer(Counted(await server.ProfileSummary(uri, cancellationToken), server.ProfileAnswer));
    }
}
