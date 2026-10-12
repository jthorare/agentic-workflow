using Aw.Kernel.Services;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Registration;
using Aw.Sindri.Host.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests;

public sealed class HostTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-di-test");

    [Fact, Trait("Lane", "unit")]
    public void Every_registered_service_resolves_in_a_scope()
    {
        var services = Compose();
        var serviceTypes = services
            .Select(d => d.ServiceType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IService<,>))
            .ToList();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<TelemetryRuntime>();   // so the container disposes it, as Program.cs does
        using var scope = provider.CreateScope();

        Assert.NotEmpty(serviceTypes);
        Assert.All(serviceTypes, t => Assert.NotNull(scope.ServiceProvider.GetRequiredService(t)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_serve_composition_validates_and_resolves_its_background_services()
    {
        using var provider = Compose().AddSindriServe().AddOptions().BuildServiceProvider(   // AddOptions: the web host's options (Kestrel's, for ServePort)
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<TelemetryRuntime>();

        var hosted = provider.GetServices<IHostedService>().ToList();

        Assert.Equal(3, hosted.Count);
        _ = Assert.Single(hosted.OfType<ServeListenerCheck>());
        _ = Assert.Single(hosted.OfType<SchedulerHost>());
        _ = Assert.Single(hosted.OfType<LeaseRenewer>());
    }

    [Fact, Trait("Lane", "unit")]
    public void Telemetry_is_composed_and_exports_nothing()
    {
        var services = Compose();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var runtime = provider.GetRequiredService<TelemetryRuntime>();

        Assert.False(runtime.IsExporting);
        _ = Assert.Single(services, d => d.ServiceType == typeof(ILoggerFactory));
        Assert.Same(runtime.LoggerFactory, provider.GetRequiredService<ILoggerFactory>());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_log_level_comes_from_the_environment_Compose_is_given()
    {
        using var provider = Compose(logLevel: "warning").BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<TelemetryRuntime>();

        var logger = provider.GetRequiredService<ILogger<HostTests>>();

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
    }

    private static IServiceCollection Compose(string? logLevel = null)
        => Composition.Compose(
            new ServiceCollection(),
            name => name switch { "AW_STATE_DIR" => _stateRoot, "AW_LOG_LEVEL" => logLevel, _ => null },
            new FakeTimeProvider());
}
