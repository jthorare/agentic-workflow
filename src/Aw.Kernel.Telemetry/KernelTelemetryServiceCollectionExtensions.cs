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
    public static IServiceCollection AddKernelTelemetry(this IServiceCollection services, TelemetryOptions options) => Register(services, options, deferred: false);

    /// <summary>
    /// Registers the runtime like <see cref="AddKernelTelemetry"/> but does not start it: it starts the first time something resolves <see cref="TelemetryRuntime"/>, ILoggerFactory or ILogger of T (#3727). For a one-shot client
    /// process that neither logs nor starts a span, so it never builds the tracer provider or the logger. A <see cref="TelemetryOptions.Problem"/> is still written at once, here, because the warning must print whatever the verb
    /// does; so the runtime starts now in that case. Spans started before the runtime starts are not recorded, so use this only where nothing starts one before it is needed. A process runs one runtime: a second call to either method throws.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="options">The telemetry options.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDeferredKernelTelemetry(this IServiceCollection services, TelemetryOptions options) => Register(services, options, deferred: true);

    private static IServiceCollection Register(IServiceCollection services, TelemetryOptions options, bool deferred)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (services.Any(d => d.ServiceType == typeof(TelemetryRuntime)))
        {
            throw new InvalidOperationException("AddKernelTelemetry was already called: a process runs one TelemetryRuntime.");
        }

        var start = new TelemetryStart();
        _ = services.AddSingleton(start);
        if (deferred && options.Problem is null)
        {
            _ = services.AddSingleton(_ => Started(start, options));   // the container disposes it, if it was ever started
        }
        else
        {
#pragma warning disable CA2000 // the runtime is registered as a singleton: the container disposes it once resolved (Program.cs resolves it)
            var runtime = Started(start, options);
#pragma warning restore CA2000
            _ = services.AddSingleton(_ => runtime);
        }

        _ = services.AddSingleton(static sp => sp.GetRequiredService<TelemetryRuntime>().LoggerFactory);
        _ = services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        return services;
    }

    private static TelemetryRuntime Started(TelemetryStart start, TelemetryOptions options)
    {
        start.Started = true;
        return TelemetryRuntime.Start(options);
    }
}
