using Aw.Kernel.Services;
using Aw.Sindri.Core.HomeWrites;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetHomeRank;
using Aw.Sindri.Features.GetHomeRow;
using Aw.Sindri.Features.HomeLanes;
using Aw.Sindri.Features.HomeRuns;
using Aw.Sindri.Features.ListHome;
using Aw.Sindri.Features.ListHomeDone;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Home slices (home spec §8): the four reads behind the query chain, and the collectors that raise and clear rows (E1).</summary>
internal static class HomeServiceCollectionExtensions
{
    /// <summary>Adds ListHome, GetHomeRow, ListHomeDone, GetHomeRank and the built-in collectors.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddHomeSlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<ListHomeRequest>, AcceptAllValidator<ListHomeRequest>>().AddQuery<ListHomeRequest, ListHomeResponse, ListHomeService>()
            .AddScoped<IValidator<GetHomeRowRequest>, AcceptAllValidator<GetHomeRowRequest>>().AddQuery<GetHomeRowRequest, GetHomeRowResponse, GetHomeRowService>()
            .AddScoped<IValidator<ListHomeDoneRequest>, AcceptAllValidator<ListHomeDoneRequest>>().AddQuery<ListHomeDoneRequest, ListHomeDoneResponse, ListHomeDoneService>()
            .AddScoped<IValidator<GetHomeRankRequest>, AcceptAllValidator<GetHomeRankRequest>>().AddQuery<GetHomeRankRequest, GetHomeRankResponse, GetHomeRankService>()
            .AddScoped<HomeRowWriter>()
            .AddScoped<IScheduledService, HomeRunsService>().AddSingleton(HomeRunsService.BuiltIn)   // 'home-runs' (E1): failed runs the owner has not seen
            .AddScoped<IScheduledService, HomeLanesService>().AddSingleton(HomeLanesService.BuiltIn);   // 'home-lanes' (E1): lanes, limits and the merge queue from aw-meta
    }
}
