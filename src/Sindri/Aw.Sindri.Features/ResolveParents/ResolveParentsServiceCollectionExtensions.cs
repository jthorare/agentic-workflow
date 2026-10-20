using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.ResolveParents;

/// <summary>Registers the parent resolution (LN-PKT6).</summary>
internal static class ResolveParentsServiceCollectionExtensions
{
    /// <summary>Adds 'resolve parents' as a scoped service.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddResolveParentsSlices(this IServiceCollection services) => services
        .AddScoped<IService<ResolveParentsRequest, ResolveParentsResponse>, ResolveParentsService>();
}
