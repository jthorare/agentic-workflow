using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Plan;

/// <summary>Registers the plan station's services (line spec §6.3).</summary>
internal static class PlanServiceCollectionExtensions
{
    /// <summary>Adds 'plan check' as a scoped service.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPlanSlices(this IServiceCollection services) => services
        .AddScoped<IService<PlanCheckRequest, LineCheckResponse>, PlanCheckService>()
        .AddScoped<IService<PlanDeskRequest, LineDeskResponse>, PlanDeskService>();
}
