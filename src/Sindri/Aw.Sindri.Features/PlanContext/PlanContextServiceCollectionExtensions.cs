using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.PlanContext;

/// <summary>Registers the plan station's context calls (LN-CTX1).</summary>
internal static class PlanContextServiceCollectionExtensions
{
    /// <summary>Adds 'propose plan context' as a scoped service.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPlanContextSlices(this IServiceCollection services) => services
        .AddScoped<IService<ProposePlanContextRequest, ProposePlanContextResponse>, ProposePlanContextService>();
}
