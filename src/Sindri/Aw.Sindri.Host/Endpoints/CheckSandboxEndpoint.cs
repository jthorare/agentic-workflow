using Aw.Kernel.Services;
using Aw.Sindri.Features.CheckSandbox;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/sandbox/check (sandbox spec §8): Check again, read-only: every blocker in the owner's words, and the Steps a resume would start. It resumes nothing.</summary>
internal static class CheckSandboxEndpoint
{
    internal static RouteGroupBuilder MapCheckSandbox(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sandbox/check", static async (IService<CheckSandboxRequest, CheckSandboxResponse> service, CancellationToken ct) =>
            (await service.Handle(new CheckSandboxRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
