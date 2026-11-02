using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Registration;
using Aw.Sindri.Infrastructure.Sandbox;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Sandbox.ProcessGroup;

/// <summary>macOS runs the limit watchdog over process groups, Linux over systemd (SB-9c); session wrap deletes the group id file when the session is over.</summary>
[Lane(TestLane.Unit)]
public sealed class ProcessGroupWatchdogPortsTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-pgports-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private ServiceProvider Provider(bool mac) =>
        new ServiceCollection().AddSingleton(new LauncherOptions(_home)).AddScopeWatch(mac).BuildServiceProvider();

    [Fact]
    public void On_a_mac_the_scope_control_the_stats_and_the_memory_kills_are_the_process_group_ones()
    {
        using var provider = Provider(mac: true);

        var control = provider.GetRequiredService<IScopeControl>();
        _ = Assert.IsType<ProcessGroupScopeControl>(control);
        Assert.Same(control, provider.GetRequiredService<IMemoryKills>());
        _ = Assert.IsType<ProcessGroupStats>(provider.GetRequiredService<ICgroupStats>());
    }

    [Fact]
    public void On_linux_it_stays_systemd_and_cgroup_files_with_no_memory_kills_port()
    {
        using var provider = Provider(mac: false);

        _ = Assert.IsType<SystemctlScopeControl>(provider.GetRequiredService<IScopeControl>());
        _ = Assert.IsType<CgroupFiles>(provider.GetRequiredService<ICgroupStats>());
        Assert.Null(provider.GetService<IMemoryKills>());
    }

    [Fact]
    public void Session_wrap_on_a_mac_kills_for_memory_itself_and_releases_the_group_id_file()
    {
        using var provider = Provider(mac: true);
        var directory = ProcessGroupFiles.Directory(new LauncherOptions(_home).LaunchDirectory);
        ProcessGroupFiles.WritePgid(directory, "LCH-a", 424242);

        var ports = SessionWrapVerb.Ports(provider, mac: true);

        Assert.NotNull(ports.MemoryKills);
        Assert.NotNull(ports.Release);
        ports.Release("LCH-a");
        Assert.Null(ProcessGroupFiles.ReadPgid(directory, "LCH-a"));
    }

    [Fact]
    public void Session_wrap_on_linux_has_neither()
    {
        using var provider = Provider(mac: false);

        var ports = SessionWrapVerb.Ports(provider, mac: false);

        Assert.Null(ports.MemoryKills);
        Assert.Null(ports.Release);
    }
}
