using Aw.Kernel.Services;
using Aw.Sindri.Features.GetHomeRow;
using Aw.Sindri.Features.ListHome;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Home slices (home spec §8): the two reads behind the query chain.</summary>
internal static class HomeServiceCollectionExtensions
{
    /// <summary>Adds ListHome and GetHomeRow.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddHomeSlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<ListHomeRequest>, AcceptAllValidator<ListHomeRequest>>().AddQuery<ListHomeRequest, ListHomeResponse, ListHomeService>()
            .AddScoped<IValidator<GetHomeRowRequest>, AcceptAllValidator<GetHomeRowRequest>>().AddQuery<GetHomeRowRequest, GetHomeRowResponse, GetHomeRowService>();
    }
}
