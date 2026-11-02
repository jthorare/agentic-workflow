using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The SB-5a switch: bubblewrap, the session cap and its lock on Linux; nothing replaced elsewhere.</summary>
[Lane(TestLane.Unit)]
public sealed class PlatformSandboxRegistrationTests
{
    private static IServiceCollection Core() => new ServiceCollection()
        .AddSingleton<ISandbox, UnavailableSandbox>()
        .AddSingleton(new SindriSettings("/home/owner/.agentic-workflow/sindri-app", "/home/owner/.agentic-workflow/sindri-app/profile.yaml"))
        .AddSingleton<TimeProvider>(new FakeTimeProvider());

    private static string? Env(string name) => string.Equals(name, "HOME", StringComparison.Ordinal) ? "/home/owner" : null;

    [Fact]
    public void Linux_swaps_UnavailableSandbox_for_bubblewrap_and_registers_the_cap()
    {
        var services = Core().AddSindriPlatformSandbox(Env, linux: true, uid: 1000);
        using var provider = services.BuildServiceProvider();

        _ = Assert.Single(services, d => d.ServiceType == typeof(ISandbox));
        _ = Assert.IsType<BubblewrapSandbox>(provider.GetRequiredService<ISandbox>());
        _ = Assert.IsType<FileSessionCap>(provider.GetRequiredService<ISessionCap>());
        _ = Assert.IsType<CgroupSliceScopes>(provider.GetRequiredService<ISliceScopes>());
        Assert.Same(provider.GetRequiredService<LaunchSandboxRegistry>(), provider.GetRequiredService<LaunchSandboxRegistry>());
        Assert.EndsWith("/sandbox/egress", PlatformSandboxServiceCollectionExtensions.EgressLogDirectory("/h"), StringComparison.Ordinal);
    }

    [Fact]
    public void Other_platforms_keep_UnavailableSandbox_and_register_no_cap()
    {
        var services = Core().AddSindriPlatformSandbox(Env, linux: false, uid: 501);
        using var provider = services.BuildServiceProvider();

        _ = Assert.IsType<UnavailableSandbox>(provider.GetRequiredService<ISandbox>());
        Assert.Null(provider.GetService<ISessionCap>());
    }

    [Fact]
    public void The_lock_sits_under_the_real_home_and_the_slice_under_the_users_manager()
    {
        Assert.Equal("/home/owner/.agentic-workflow/sindri-app/locks/sessions.lock", PlatformSandboxServiceCollectionExtensions.LockFile("/home/owner/.agentic-workflow/sindri-app"));
        Assert.Equal("/sys/fs/cgroup/user.slice/user-1000.slice/user@1000.service/sindri.slice/sindri-steps.slice", PlatformSandboxServiceCollectionExtensions.SliceDirectory(1000));
    }
}
