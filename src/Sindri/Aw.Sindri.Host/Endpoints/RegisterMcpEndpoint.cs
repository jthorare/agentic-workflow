using Aw.Sindri.Features.RegisterMcp;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/setup/mcp (setup-once spec §5.4, §7.2): register Sindri in a repo from the app. The body is '{repo?}'; the cheap check answers 409 SND-SETUP-004, then the job starts like every job route.</summary>
internal static class RegisterMcpEndpoint
{
    internal static RouteGroupBuilder MapRegisterMcp(this RouteGroupBuilder group) => group.MapJob<RegisterMcpJobRequest, RegisterMcpResponse>("/setup/mcp");
}
