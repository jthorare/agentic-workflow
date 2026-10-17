using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Core;

/// <summary>Registers Aw.Forge.Core's types.</summary>
public static class ForgeCoreServiceCollectionExtensions
{
    /// <summary>Adds the Forge domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddForgeCore(this IServiceCollection services) => services;
}
