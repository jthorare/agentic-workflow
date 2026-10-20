using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Design;

/// <summary>Registers the design station's services (line spec §4.3).</summary>
internal static class DesignServiceCollectionExtensions
{
    /// <summary>Adds 'design check' as a scoped service.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddDesignSlices(this IServiceCollection services) => services
        .AddScoped<IService<DesignCheckRequest, LineCheckResponse>, DesignCheckService>()
        .AddScoped<IService<DesignDeskRequest, LineDeskResponse>, DesignDeskService>()
        .AddScoped<IService<DesignShowRequest, DesignStopView>, DesignShowService>()
        .AddScoped<PlanInstances>();
}
