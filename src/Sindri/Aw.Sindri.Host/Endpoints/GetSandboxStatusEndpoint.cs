using Aw.Kernel.Services;
using Aw.Sindri.Features.GetSandboxStatus;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/sandbox/status (sandbox spec §8): what the last check found and how the sandbox is set up, for the Health row and the Settings section. Cheap: it runs no probe.</summary>
internal static class GetSandboxStatusEndpoint
{
    internal static RouteGroupBuilder MapGetSandboxStatus(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sandbox/status", static async (IService<GetSandboxStatusRequest, SandboxStatusResponse> service, CancellationToken ct) =>
            (await service.Handle(new GetSandboxStatusRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
