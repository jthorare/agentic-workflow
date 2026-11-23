using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetHomeRank;
using Aw.Sindri.Features.GetHomeRow;
using Aw.Sindri.Features.ListHome;
using Aw.Sindri.Features.ListHomeDone;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Home slices (home spec §8): the four reads behind the query chain.</summary>
internal static class HomeServiceCollectionExtensions
{
    /// <summary>Adds ListHome, GetHomeRow, ListHomeDone and GetHomeRank.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddHomeSlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<ListHomeRequest>, AcceptAllValidator<ListHomeRequest>>().AddQuery<ListHomeRequest, ListHomeResponse, ListHomeService>()
            .AddScoped<IValidator<GetHomeRowRequest>, AcceptAllValidator<GetHomeRowRequest>>().AddQuery<GetHomeRowRequest, GetHomeRowResponse, GetHomeRowService>()
            .AddScoped<IValidator<ListHomeDoneRequest>, AcceptAllValidator<ListHomeDoneRequest>>().AddQuery<ListHomeDoneRequest, ListHomeDoneResponse, ListHomeDoneService>()
            .AddScoped<IValidator<GetHomeRankRequest>, AcceptAllValidator<GetHomeRankRequest>>().AddQuery<GetHomeRankRequest, GetHomeRankResponse, GetHomeRankService>();
    }
}
