using Aw.Kernel.Services;
using Aw.Kernel.Telemetry;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests;

public sealed class HostTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-meta-di-test");

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

    // LaneAddress is an optional constructor parameter: without its registration the settler matches by name only and every
    // settler test, which builds it by hand, stays green (#4181).
    [Fact, Trait("Lane", "unit")]
    public void The_recycle_settler_from_the_container_matches_the_lane_by_its_socket()
    {
        using var provider = Compose().BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<TelemetryRuntime>();
        using var scope = provider.CreateScope();

        var settler = scope.ServiceProvider.GetRequiredService<RecycleSettler>();

        Assert.True(settler.MatchesBySocket);
    }

    // #4332: InboxSync is an optional constructor parameter, so without it the readiness check would read only the stored
    // messages and every merge-check test that builds it by hand would stay green.
    [Fact, Trait("Lane", "unit")]
    public void The_merge_readiness_from_the_container_syncs_the_meta_transcript()
    {
        using var provider = Compose().BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<TelemetryRuntime>();
        using var scope = provider.CreateScope();

        var readiness = scope.ServiceProvider.GetRequiredService<MergeReadiness>();

        Assert.True(readiness.Syncs);
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
            name => name switch { "AW_STATE_DIR" => _stateRoot, "AW_LOG_LEVEL" => logLevel, "AW_TMUX" => Path.Combine(_stateRoot, "no-tmux-here"), _ => null },
            new FakeTimeProvider());
}
