using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.BuildToolkit;
using Aw.Sindri.Features.ListToolkits;
using Aw.Sindri.Features.StartToolkit;
using Aw.Sindri.Features.StopToolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the toolkit slices (agent-first PR 16): the build job, start, stop and list.</summary>
internal static class ToolkitServiceCollectionExtensions
{
    /// <summary>Adds the toolkit slices. The build job is keyed by toolkit name, so one toolkit builds at a time.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddToolkits(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddJob<BuildToolkitRequest, BuildToolkitResult, BuildToolkitService>(static r => JobKey.From("toolkit:" + r.Name), static r => "built " + r.Name)
            .AddScoped<IService<StartToolkitRequest, StartToolkitResponse>, StartToolkitService>()
            .AddScoped<IService<StopToolkitRequest, StopToolkitResponse>, StopToolkitService>()
            .AddScoped<IService<ListToolkitsRequest, ListToolkitsResponse>, ListToolkitsService>();
    }
}
