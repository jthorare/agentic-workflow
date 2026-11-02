using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
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
    public void Mac_leaves_the_fixture_check_to_Cores_refusing_default()
    {
        var services = Core().AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: true);

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ISandboxFixture));
    }

    [Fact]
    public void On_a_mac_sandbox_login_is_registered()
    {
        using var provider = Core().AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: true).BuildServiceProvider();

        Assert.NotNull(provider.GetService<ISandboxLogin>());
    }

    [Fact]
    public void On_a_mac_sandbox_exec_is_registered()
    {
        using var provider = Core()
            .AddSingleton<ISandboxProcesses, SandboxProcesses>()
            .AddSingleton<ISandboxMachine>(new Machine())
            .AddSingleton<IApprovedProfile, NoApprovedProfile>()
            .AddSingleton<ILaunchSecrets>(_ => throw new NotSupportedException("not needed to resolve the exec"))
            .AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: true)
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<ISandboxExec>());
    }

    [Fact]
    public void Exec_scope_on_a_mac_is_its_own_process_group_through_the_perl_wrapper()
    {
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        IReadOnlyList<string> inner = ["sandbox-exec", "-f", "p", "claude"];

        var argv = ExecScope.Mac("/pg")(launch, Limits(), inner);

        // why: a Step's scope script needs a session leader (tmux gives one); exec is a plain child, so the wrapper makes its own group (#1940 review)
        Assert.Equal(["/usr/bin/perl", "-e", ExecScope.MacScript, ProcessGroupFiles.PgidPath("/pg", launch.Value)!, .. inner], argv);
        Assert.DoesNotContain("systemd-run", argv, StringComparer.Ordinal);
    }

    [Fact]
    public void Exec_scope_on_linux_is_the_systemd_one()
    {
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        IReadOnlyList<string> inner = ["bwrap", "--", "claude"];

        var argv = ExecScope.Linux("systemd-run")(launch, Limits(), inner);

        Assert.Equal(SystemdScopeArgv.Start("systemd-run", launch, Limits(), inner), argv);
    }

    private static ScopeLimits Limits() =>
        ScopeLimits.From(new SandboxLimits(Memory: "8GiB", Total: "20GiB"), new BoxFacts(32L << 30, 4), TimeSpan.FromMinutes(10), TimeSpan.FromHours(24), attended: false);

    private sealed class Machine : ISandboxMachine
    {
        public long RamBytes => 16L << 30;

        public int Cores => 8;
    }

    private sealed class NoApprovedProfile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<LoadedProfile?>(value: null));
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
