using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Core;

/// <summary>Registers Aw.Connectors.Core's types.</summary>
public static class ConnectorsCoreServiceCollectionExtensions
{
    /// <summary>Adds the Connectors domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddConnectorsCore(this IServiceCollection services) => services;
}
