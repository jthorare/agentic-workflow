using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Seatbelt;

/// <summary>The macOS switch (SB-9c): Seatbelt, the process-group cap and the loopback egress on a Mac; nothing new elsewhere.</summary>
[Lane(TestLane.Unit)]
public sealed class SeatbeltRegistrationTests
{
    private static IServiceCollection Core() => new ServiceCollection()
        .AddSingleton<ISandbox, UnavailableSandbox>()
        .AddSingleton(new SindriSettings("/Users/owner/.agentic-workflow/sindri-app", "/Users/owner/.agentic-workflow/sindri-app/profile.yaml"))
        .AddSingleton(new LauncherOptions("/Users/owner/.agentic-workflow/sindri-app/launcher"))
        .AddSingleton<TimeProvider>(new FakeTimeProvider());

    private static string? Env(string name) => string.Equals(name, "HOME", StringComparison.Ordinal) ? "/Users/owner" : null;

    [Fact]
    public void Mac_swaps_UnavailableSandbox_for_Seatbelt_with_the_process_group_cap()
    {
        var services = Core().AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: true);
        using var provider = services.BuildServiceProvider();

        _ = Assert.Single(services, d => d.ServiceType == typeof(ISandbox));
        _ = Assert.IsType<SeatbeltSandbox>(provider.GetRequiredService<ISandbox>());
        _ = Assert.IsType<ProcessGroupSliceScopes>(provider.GetRequiredService<ISliceScopes>());
        _ = Assert.IsType<FileSessionCap>(provider.GetRequiredService<ISessionCap>());
        Assert.Same(provider.GetRequiredService<LaunchSandboxRegistry>(), provider.GetRequiredService<LaunchSandboxRegistry>());
    }

    [Fact]
    public void Mac_leaves_the_fixture_check_and_the_exec_and_login_verbs_to_Cores_refusing_defaults()
    {
        var services = Core().AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: true);

        Type[] verbs = [typeof(ISandboxFixture), typeof(ISandboxExec), typeof(ISandboxLogin)];
        Assert.DoesNotContain(services, d => verbs.Contains(d.ServiceType));
    }

    [Fact]
    public void Neither_linux_nor_mac_registers_nothing_new()
    {
        var services = Core().AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: false);
        using var provider = services.BuildServiceProvider();

        _ = Assert.IsType<UnavailableSandbox>(provider.GetRequiredService<ISandbox>());
        Assert.Null(provider.GetService<ISessionCap>());
        Assert.Null(provider.GetService<ISliceScopes>());
    }

    [Fact]
    public void Seatbelt_options_read_the_redirect_variable_or_default_to_the_system_binary()
    {
        Assert.Equal("/usr/bin/sandbox-exec", SeatbeltOptions.From(static _ => null).SandboxExec);
        Assert.Equal("/x/sandbox-exec", SeatbeltOptions.From(static n => string.Equals(n, SandboxEnvironment.SeatbeltExec, StringComparison.Ordinal) ? "/x/sandbox-exec" : null).SandboxExec);
    }
}
