using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The setup writes of the fake handler (setup-once spec §7.2): the repo and the agents' registration.</summary>
internal sealed partial class FakeSindriHandler
{
    private static readonly string[] _setupWrites = [SetupRoutes.Repos, SetupRoutes.Mcp];

    private static bool IsSetupWrite(string path) => _setupWrites.Contains(path, StringComparer.Ordinal);

    private async Task<HttpResponseMessage> SetupWrite(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        return path switch
        {
            SetupRoutes.Repos => Answer(Counted(await server.AddRepo(uri, await Body<AddRepoRequest>(request, cancellationToken), cancellationToken), server.AddRepoAnswer)),
            _ => Answer(Counted(await server.RegisterMcp(uri, await Body<RegisterMcpRequest>(request, cancellationToken), cancellationToken), server.RegisterMcpAnswer)),
        };
    }
}
