using Aw.Sindri.Features.OnboardRepo;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/setup/repos/onboard (setup-once spec §5.1): onboard a repo of the approved profile by hand. The body is '{path}'; the cheap checks answer 409 (SND-SETUP-004, 007), then 200 with the job, like every job route. It never approves the profile.</summary>
internal static class OnboardRepoEndpoint
{
    internal static RouteGroupBuilder MapOnboardRepo(this RouteGroupBuilder group) => group.MapJob<OnboardRepoRequest, OnboardedRepo>("/setup/repos/onboard");
}
