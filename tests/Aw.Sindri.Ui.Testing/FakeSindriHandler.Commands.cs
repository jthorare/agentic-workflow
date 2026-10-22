using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The workflow command routes of <see cref="FakeSindriHandler"/>: the desk report.</summary>
internal sealed partial class FakeSindriHandler
{
    private static bool IsComplete(HttpRequestMessage request, string path) =>
        request.Method == HttpMethod.Post && path.StartsWith("/sindri/workflow/instances/", StringComparison.Ordinal) && path.EndsWith("/complete", StringComparison.Ordinal);

    private async Task<HttpResponseMessage> CompleteRoute(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var parts = path["/sindri/workflow/instances/".Length..].Split('/').Select(Uri.UnescapeDataString).ToArray();   // {id}/runs/{run}/complete
        var body = await Body<CompleteStepBody>(request, cancellationToken);
        return Answer(Counted(await server.CompleteStep(request.RequestUri!, parts[0], body with { Instance = parts[0], Run = parts[2] }, cancellationToken), server.CompleteAnswer));   // why: the route wins over the body's, as on the server
    }

    private static bool IsCommand(HttpRequestMessage request, string path) =>
        request.Method == HttpMethod.Post && path.StartsWith("/sindri/workflow/instances/", StringComparison.Ordinal) && path.EndsWith("/commands", StringComparison.Ordinal);

    private async Task<HttpResponseMessage> CommandRoute(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var id = Uri.UnescapeDataString(path["/sindri/workflow/instances/".Length..^"/commands".Length]);
        var body = await Body<ApplyCommandBody>(request, cancellationToken);
        return Answer(Counted(await server.ApplyCommand(request.RequestUri!, id, body with { Instance = id }, cancellationToken), server.CommandAnswer));   // why: the route wins over the body's, as on the server
    }
}
