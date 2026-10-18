using Aw.Meta.Core.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>The one place that picks the host adapters (process table, host resources, process liveness, process locations) for an OS.</summary>
internal static class HostAdapterServiceCollectionExtensions
{
    /// <summary>
    /// Registers IProcessTable, IHostResources, IProcessLiveness and IProcessLocations for the platform: /proc on Linux, sysctl,
    /// vm_stat, ps and lsof on macOS, and on any other OS an adapter that answers AW-META-010 naming it. It needs ICommandRunner and TimeProvider registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="platform">The OS.</param>
    /// <param name="stateDirectory">A directory on the disk whose free space is reported.</param>
    /// <param name="heavyLock">The heavy-job lock path.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddHostAdapters(this IServiceCollection services, HostPlatform platform, string stateDirectory, string heavyLock)
    {
        ArgumentNullException.ThrowIfNull(platform);
        _ = services.AddSingleton(MacToolPaths.Default);
        switch (platform.Kind)
        {
            case HostKind.Mac:
                return services
                    .AddSingleton(sp => new MacHostReader(stateDirectory, sp.GetRequiredService<ICommandRunner>(), sp.GetRequiredService<MacToolPaths>(), DiskSpace.Free))
                    .AddSingleton<IProcessLiveness>(sp => new MacProcessLiveness(sp.GetRequiredService<MacHostReader>(), sp.GetRequiredService<TimeProvider>()))
                    .AddSingleton<IProcessTable>(sp => new MacProcessTable(sp.GetRequiredService<MacHostReader>()))
                    .AddSingleton<IProcessLocations>(sp => new MacProcessLocations(sp.GetRequiredService<MacHostReader>(), Environment.ProcessId))
                    .AddSingleton<IHostResources>(sp => new MacHostResources(sp.GetRequiredService<MacHostReader>(), heavyLock, sp.GetRequiredService<IProcessLiveness>()));
            case HostKind.Linux:
                return services
                    .AddSingleton<IProcessLiveness>(sp => new ProcProcessLiveness(sp.GetRequiredService<TimeProvider>()))
                    .AddSingleton<IProcessTable>(new ProcProcessTable(stateDirectory))
                    .AddSingleton<IProcessLocations>(new ProcProcessLocations())
                    .AddSingleton<IHostResources>(sp => new ProcHostResources(stateDirectory, heavyLock, sp.GetRequiredService<IProcessLiveness>()));
            case HostKind.Unsupported:
            default:
                var unsupported = new UnsupportedHost(platform.Name);
                return services.AddSingleton<IProcessLiveness>(unsupported).AddSingleton<IProcessTable>(unsupported).AddSingleton<IHostResources>(unsupported).AddSingleton<IProcessLocations>(unsupported);
        }
    }
}
