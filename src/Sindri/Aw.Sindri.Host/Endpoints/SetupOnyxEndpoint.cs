using Aw.Sindri.Features.SetupOnyx;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/onyx/setup (setup-once spec §5.6; desk): starts the Onyx setup as a job on the generic job route. It takes no input: any body, or none, starts it.</summary>
internal static class SetupOnyxEndpoint
{
    internal static RouteGroupBuilder MapSetupOnyx(this RouteGroupBuilder group) =>
        group.MapJob<SetupOnyxRequest, SetupOnyxResult>("/onyx/setup", whenEmpty: static () => new SetupOnyxRequest());
}
