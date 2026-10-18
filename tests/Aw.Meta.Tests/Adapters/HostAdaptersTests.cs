using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Adapters;

/// <summary>The one place that picks the host adapters for an OS: both branches, whatever OS the tests run on.</summary>
public sealed class HostAdaptersTests
{
    private static ServiceProvider Resolve(HostPlatform platform) =>
        new ServiceCollection()
            .AddSingleton<ICommandRunner>(new InMemoryCommandRunner())
            .AddSingleton<TimeProvider>(new FakeTimeProvider())
            .AddHostAdapters(platform, "/state", "/state/locks/heavy-job.lock")
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    [Fact, Trait("Lane", "unit")]
    public void Linux_reads_proc()
    {
        using var provider = Resolve(new HostPlatform(HostKind.Linux, "Linux"));

        _ = Assert.IsType<ProcProcessTable>(provider.GetRequiredService<IProcessTable>());
        _ = Assert.IsType<ProcHostResources>(provider.GetRequiredService<IHostResources>());
        _ = Assert.IsType<ProcProcessLiveness>(provider.GetRequiredService<IProcessLiveness>());
        _ = Assert.IsType<ProcProcessLocations>(provider.GetRequiredService<IProcessLocations>());
    }

    [Fact, Trait("Lane", "unit")]
    public void Mac_reads_the_macOS_tools()
    {
        using var provider = Resolve(new HostPlatform(HostKind.Mac, "macOS"));

        _ = Assert.IsType<MacProcessTable>(provider.GetRequiredService<IProcessTable>());
        _ = Assert.IsType<MacHostResources>(provider.GetRequiredService<IHostResources>());
        _ = Assert.IsType<MacProcessLiveness>(provider.GetRequiredService<IProcessLiveness>());
        _ = Assert.IsType<MacProcessLocations>(provider.GetRequiredService<IProcessLocations>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Another_OS_gets_adapters_that_answer_AW_META_010_naming_it()
    {
        await using var provider = Resolve(new HostPlatform(HostKind.Unsupported, "FreeBSD 14.1"));

        var snapshot = await provider.GetRequiredService<IProcessTable>().Snapshot(TestContext.Current.CancellationToken);
        var resources = await provider.GetRequiredService<IHostResources>().Read(TestContext.Current.CancellationToken);
        var locks = await provider.GetRequiredService<IHostResources>().HeavyLocks(TestContext.Current.CancellationToken);
        var alive = await provider.GetRequiredService<IProcessLiveness>().IsRunning(5, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
        var stop = await provider.GetRequiredService<IProcessTable>().Terminate(5, TestContext.Current.CancellationToken);
        var scan = await provider.GetRequiredService<IProcessLocations>().Locations(TestContext.Current.CancellationToken);

        foreach (var error in new[] { snapshot.Error, resources.Error, locks.Error, alive.Error, stop.Error, scan.Error })
        {
            Assert.Equal(MetaErrors.HostUnreadable.Code, error.Code);
            Assert.Contains("FreeBSD 14.1", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_platform_this_process_runs_on_is_one_of_the_three()
    {
        var platform = HostPlatform.Current;

        Assert.Equal(
            OperatingSystem.IsMacOS() ? HostKind.Mac : OperatingSystem.IsLinux() ? HostKind.Linux : HostKind.Unsupported,
            platform.Kind);
    }
}
