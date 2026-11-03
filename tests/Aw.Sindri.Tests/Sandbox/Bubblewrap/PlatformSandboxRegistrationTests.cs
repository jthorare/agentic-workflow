using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The SB-5a switch: bubblewrap, the session cap and its lock on Linux; elsewhere UnavailableSandbox stays and a sandbox step that refuses every launch, so no Step runs bare (#2016).</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
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

    private static LaunchRequest Step(string dir) =>
        A.LaunchRequest().InDirectory(dir).Unattended(new StepArgs(Model: null, Policy: null, Ref: new StepRef("build", "agent"))).Build();

    [Fact]
    public async Task Other_platforms_register_a_sandbox_step_that_refuses_every_launch_with_014()
    {
        await using var provider = Core().AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: false).BuildServiceProvider();
        var dir = Directory.CreateTempSubdirectory("aw-other-os-step-").FullName;
        try
        {
            var step = provider.GetService<ISandboxLaunch>();

            Assert.NotNull(step);
            var request = Step(dir);
            var prepared = await step.Prepare(request, ["claude"], chosenSession: null, CancellationToken.None);
            Assert.True(prepared.IsError);
            Assert.Equal("SND-LAUNCH-014", prepared.Error.Code);
            Assert.Equal(SandboxReasons.UnsupportedPlatform, SandboxRefusal.ReasonOf(prepared.Error));
            await step.Release(request.Launch, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task On_other_platforms_a_Step_launch_starts_no_agent()
    {
        await using var provider = Core().AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: false).BuildServiceProvider();
        var home = Directory.CreateTempSubdirectory("aw-other-os-home-").FullName;
        var work = Directory.CreateTempSubdirectory("aw-other-os-work-").FullName;
        try
        {
            var host = new FakeSessionHost();
            var parts = new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => host, new FakeTerminalViewer(), new LauncherOptions(home) { SindriHome = "/real/sindri-home" }, new FakeTimeProvider(KnownInstants.Epoch))
            {
                Sandbox = provider.GetService<ISandboxLaunch>(),
                ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
            };

            var result = await new Launcher(parts).Launch(Step(work), CancellationToken.None);

            Assert.True(result is FailedLaunch failed && string.Equals(failed.Error.Code, "SND-LAUNCH-014", StringComparison.Ordinal));
            Assert.Empty(host.Started);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
            Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public void The_lock_sits_under_the_real_home_and_the_slice_under_the_users_manager()
    {
        Assert.Equal("/home/owner/.agentic-workflow/sindri-app/locks/sessions.lock", PlatformSandboxServiceCollectionExtensions.LockFile("/home/owner/.agentic-workflow/sindri-app"));
        Assert.Equal("/sys/fs/cgroup/user.slice/user-1000.slice/user@1000.service/sindri.slice/sindri-steps.slice", PlatformSandboxServiceCollectionExtensions.SliceDirectory(1000));
    }
}
