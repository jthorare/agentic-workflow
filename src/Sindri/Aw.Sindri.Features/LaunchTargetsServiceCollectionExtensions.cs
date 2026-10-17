using System.Globalization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ProbeLaunchTargets;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the launch-target probe on the P6 job runtime (API P10a, UI spec E-3): one probe at a time.</summary>
internal static class LaunchTargetsServiceCollectionExtensions
{
    internal static IServiceCollection AddLaunchTargetJob(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddJob<ProbeLaunchTargetsRequest, ProbeLaunchTargetsResult, ProbeLaunchTargetsService>(static _ => JobKey.From("probe"), static r => Summary(r));
    }

    private static string Summary(ProbeLaunchTargetsResult result) =>
        string.Create(CultureInfo.InvariantCulture, $"{result.Present} of {result.Targets} targets present{(result.Changed ? " (changed)" : string.Empty)}");
}
