using Aw.Sindri.Ui.Api.Sandbox;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The sandbox routes of the fake handler (SB-6): status, check and denials reads, and the Allow write. They share one arm with the jobs routes so <c lang="text">Route</c> stays under the complexity limit.</summary>
internal sealed partial class FakeSindriHandler
{
    private static bool IsJobsOrSandbox(string path) =>
        path == "/sindri/jobs" || path.StartsWith("/sindri/jobs/", StringComparison.Ordinal) || path.StartsWith("/sindri/sandbox/", StringComparison.Ordinal);

    private async Task<HttpResponseMessage> JobsOrSandbox(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        return path switch
        {
            "/sindri/sandbox/status" when request.Method == HttpMethod.Get => Answer(Counted(await server.SandboxStatus(uri, cancellationToken), server.SandboxStatusAnswer)),
            "/sindri/sandbox/check" when request.Method == HttpMethod.Get => Answer(Counted(await server.SandboxCheck(uri, cancellationToken), server.SandboxCheckAnswer)),
            "/sindri/sandbox/denials" when request.Method == HttpMethod.Get => Answer(Counted(await server.SandboxDenials(uri, Query(uri, "item") ?? string.Empty, cancellationToken), server.SandboxDenialsAnswer)),
            "/sindri/sandbox/allow" when request.Method == HttpMethod.Post =>
                Answer(Counted(await server.SandboxAllow(uri, await Body<AllowRequest>(request, cancellationToken), cancellationToken), server.SandboxAllowAnswer)),
            _ when path.StartsWith("/sindri/jobs", StringComparison.Ordinal) => await Jobs(request, cancellationToken),
            _ => Text(System.Net.HttpStatusCode.NotFound, "{}", "application/json"),
        };
    }
}
