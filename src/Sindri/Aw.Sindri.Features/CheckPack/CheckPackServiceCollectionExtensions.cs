using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.CheckPack;

/// <summary>'sindri pack check' (line spec §5.3), in process.</summary>
internal static class CheckPackServiceCollectionExtensions
{
    /// <summary>Registers the pack check.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddCheckPack(this IServiceCollection services) => services
        .AddScoped<IService<CheckPackRequest, CheckPackResponse>, CheckPackService>();
}
