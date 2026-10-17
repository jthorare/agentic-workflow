using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Refs;
using Aw.Forge.Host;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Forge.Tests;

public sealed class HostTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-forge-di-test");

    [Fact, Trait("Lane", "unit")]
    public void Every_registered_service_resolves_in_a_validated_container()
    {
        var services = Compose();
        var serviceTypes = services.Select(d => d.ServiceType).Where(t => !t.ContainsGenericParameters).Distinct().ToList();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<TelemetryRuntime>();   // so the container disposes it, as Program.cs does
        using var scope = provider.CreateScope();

        Assert.Contains(typeof(IForge), serviceTypes);
        Assert.Contains(typeof(IForgeSeed), serviceTypes);
        Assert.All(serviceTypes, t => Assert.NotNull(scope.ServiceProvider.GetRequiredService(t)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_file_adapter_is_the_default_and_github_is_chosen_by_the_environment()
    {
        using var file = Compose().BuildServiceProvider();
        using var github = Compose(adapter: "github").BuildServiceProvider();

        Assert.Equal(ForgeKind.File, file.GetRequiredService<IForge>().Kind);
        Assert.Equal(ForgeKind.GitHub, github.GetRequiredService<IForge>().Kind);
        Assert.NotNull(file.GetService<IForgeSeed>());
        Assert.Null(github.GetService<IForgeSeed>());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bad_adapter_or_credential_is_refused_at_composition()
    {
        _ = Assert.Throws<InvalidOperationException>(() => Compose(adapter: "gitlab"));
        _ = Assert.Throws<InvalidOperationException>(() => Compose(adapter: "github", credential: "not-a-credential"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_github_adapter_refuses_more_than_one_host_because_its_credential_is_bound_to_one()
    {
        _ = Assert.Throws<InvalidOperationException>(() => Compose(adapter: "github", hosts: "github.com,ghe.example"));

        using var one = Compose(adapter: "github", hosts: "ghe.example").BuildServiceProvider();
        Assert.Equal(ForgeKind.GitHub, one.GetRequiredService<IForge>().Kind);
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

    private static IServiceCollection Compose(string? logLevel = null, string? adapter = null, string? credential = null, string? hosts = null)
        => Composition.Compose(
            new ServiceCollection(),
            name => name switch
            {
                "AW_STATE_DIR" => _stateRoot,
                "AW_LOG_LEVEL" => logLevel,
                "AW_FORGE_ADAPTER" => adapter,
                "AW_FORGE_CREDENTIAL" => credential,
                "AW_FORGE_HOSTS" => hosts,
                _ => null,
            },
            new FakeTimeProvider());
}
