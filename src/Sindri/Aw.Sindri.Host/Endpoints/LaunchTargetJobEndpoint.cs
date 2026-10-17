using Aw.Sindri.Features.ProbeLaunchTargets;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/launch/targets/probe (UI spec E-3): checks the launch targets again, as a job on the generic job route.</summary>
internal static class LaunchTargetJobEndpoint
{
    internal static RouteGroupBuilder MapLaunchTargetJob(this RouteGroupBuilder group) =>
        group.MapJob<ProbeLaunchTargetsRequest, ProbeLaunchTargetsResult>("/launch/targets/probe", whenEmpty: static () => new ProbeLaunchTargetsRequest());   // the probe takes no input: any body, or none, starts it
}
