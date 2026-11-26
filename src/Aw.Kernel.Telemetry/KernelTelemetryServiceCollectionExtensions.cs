using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry;

/// <summary>Registers Aw.Kernel.Telemetry (spec §4b.4: one explicit method per project; only the Host calls it).</summary>
public static class KernelTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Starts the <see cref="TelemetryRuntime"/> now (so spans are recorded and a <see cref="TelemetryOptions.Problem"/>
    /// is written while Compose runs, before anything resolves a logger) and registers it as a singleton, with its
    /// logger factory as ILoggerFactory and ILogger of T. The container disposes the runtime once something has
    /// resolved it, so Program.cs resolves <see cref="TelemetryRuntime"/> right after building the container. A
    /// process runs one runtime: a second call throws.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="options">The telemetry options.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddKernelTelemetry(this IServiceCollection services, TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (services.Any(d => d.ServiceType == typeof(TelemetryRuntime)))
        {
            throw new InvalidOperationException("AddKernelTelemetry was already called: a process runs one TelemetryRuntime.");
        }

#pragma warning disable CA2000 // the runtime is registered as a singleton: the container disposes it once resolved (Program.cs resolves it)
        var runtime = TelemetryRuntime.Start(options);
#pragma warning restore CA2000
        _ = services.AddSingleton(new TelemetryStart { Started = true });
        _ = services.AddSingleton(_ => runtime);
        _ = services.AddSingleton(static sp => sp.GetRequiredService<TelemetryRuntime>().LoggerFactory);
        _ = services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        return services;
    }

    /// <summary>Registers the runtime without starting it (stub for the red commit: it starts at once).</summary>
    /// <param name="services">The services.</param>
    /// <param name="options">The telemetry options.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDeferredKernelTelemetry(this IServiceCollection services, TelemetryOptions options) => services.AddKernelTelemetry(options);
}
