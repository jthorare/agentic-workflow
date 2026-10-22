using Aw.Kernel.Services;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Features.SetupSources;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the setup read (setup-once spec §7.2): GetSetup and the per-step sources it decides each step from.</summary>
internal static class SetupServiceCollectionExtensions
{
    /// <summary>Adds the GetSetup query and its sources (repo, profile, recipes, mcp, map, onyx).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddGetSetup(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<GetSetupRequest>, AcceptAllValidator<GetSetupRequest>>().AddQuery<GetSetupRequest, GetSetupResponse, GetSetupService>()
            .AddScoped<ISetupFacts, SetupFacts>().AddScoped<GetNeedsYou.SetupNotices>().AddScoped<ProfileSetupReader>().AddScoped<McpSetupReader>().AddScoped<MapSetupReader>().AddScoped<OnyxStatusReader>().AddScoped<OnyxSetupReader>();
    }
}
