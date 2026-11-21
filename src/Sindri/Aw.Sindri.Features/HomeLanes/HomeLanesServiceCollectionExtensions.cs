using Aw.Sindri.Core.Lanes;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>Registers the 'home-lanes' collector (Home spec §3.3, E1).</summary>
internal static class HomeLanesServiceCollectionExtensions
{
    /// <summary>Adds the 'home-lanes' service as a built-in and the ledger-backed lane rows it writes through.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddHomeLanes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IScheduledService, HomeLanesService>()
            .AddSingleton(HomeLanesService.BuiltIn)
            .AddScoped<ILaneRows, LedgerLaneRows>();
    }
}
