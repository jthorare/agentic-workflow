using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetLaunchPicks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the engine in depth's three queries (API P13, UI E-30) and the model picks read (model-pick spec §6.1).</summary>
internal static class EngineServiceCollectionExtensions
{
    internal static IServiceCollection AddEngineReads(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<GetLaunchPicksRequest>, AcceptAllValidator<GetLaunchPicksRequest>>().AddQuery<GetLaunchPicksRequest, LaunchPicks, GetLaunchPicksService>();   // model-pick spec §6.1
    }
}
