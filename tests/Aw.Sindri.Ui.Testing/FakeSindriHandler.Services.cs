using System.Net;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The service and stop command routes of <see cref="FakeSindriHandler"/>.</summary>
internal sealed partial class FakeSindriHandler
{
    private const string ServicesRoot = "/sindri/services/";

    private static bool IsServiceCommand(HttpRequestMessage request, string path) =>
        request.Method == HttpMethod.Post
        && (string.Equals(path, ServiceRoutes.Stop, StringComparison.Ordinal)
            || (path.StartsWith(ServicesRoot, StringComparison.Ordinal) && path[ServicesRoot.Length..].Split('/').Length == 2));

    private async Task<HttpResponseMessage> ServiceCommand(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        if (string.Equals(path, ServiceRoutes.Stop, StringComparison.Ordinal))
        {
            return Answer(Counted(await server.StopSindri(uri, cancellationToken), server.StopAnswer));
        }

        var parts = path[ServicesRoot.Length..].Split('/').Select(Uri.UnescapeDataString).ToArray();   // {name}/{verb}
        return parts[1] switch
        {
            "run" => Answer(Counted(await server.RunService(uri, parts[0], cancellationToken), server.RunServiceAnswer)),
            "pause" => Answer(Counted(await server.PauseService(uri, parts[0], cancellationToken), server.PauseServiceAnswer)),
            "resume" => Answer(Counted(await server.ResumeService(uri, parts[0], cancellationToken), server.ResumeServiceAnswer)),
            _ => Text(HttpStatusCode.NotFound, "{}", "application/json"),
        };
    }
}
