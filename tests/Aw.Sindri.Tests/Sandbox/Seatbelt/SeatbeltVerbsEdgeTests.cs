using System.Diagnostics;
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
using Aw.Sindri.Tests.Sandbox.Bubblewrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Seatbelt;

/// <summary>The edges of 'sandbox exec' and 'sandbox login' on a Mac (#1936): which folder the cap counts, what exec removes, who is refused, and that nothing new appears elsewhere.</summary>
[Lane(TestLane.Integration)]
public sealed class SeatbeltVerbsEdgeTests : IDisposable
{
    private readonly string _launchDirectory = Directory.CreateTempSubdirectory("aw-seatbelt-verbs-").FullName;
    private readonly List<Process> _started = [];

    public void Dispose()
    {
        foreach (var process in _started)
        {
            if (!process.HasExited)
            {
                process.Kill();
            }

            process.Dispose();
        }

        Directory.Delete(_launchDirectory, recursive: true);
    }

    private static string? Env(string name) => string.Equals(name, "HOME", StringComparison.Ordinal) ? "/Users/owner" : null;

    private ServiceProvider MacServices() => new ServiceCollection()
        .AddSingleton<ISandbox, UnavailableSandbox>()
        .AddSingleton(new SindriSettings("/Users/owner/.agentic-workflow/sindri-app", "/Users/owner/.agentic-workflow/sindri-app/profile.yaml"))
        .AddSingleton(new LauncherOptions(_launchDirectory))
        .AddSingleton<TimeProvider>(new FakeTimeProvider())
        .AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: true)
        .BuildServiceProvider();

    [Fact]
    public async Task The_registered_cap_counts_the_group_files_in_the_folder_exec_scope_writes_to()
    {
        var setsid = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(d => Path.Combine(d, "setsid")).FirstOrDefault(File.Exists);
        Assert.SkipWhen(setsid is null, "setsid is not installed");
        await using var provider = MacServices();
        var pgroups = ProcessGroupFiles.Directory(provider.GetRequiredService<LauncherOptions>().LaunchDirectory);   // the folder the registration hands both the cap and exec's scope
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");

        // the file exec's scope tells the shell to write, and a live group for it to name
        var argv = ExecScope.Mac(pgroups)(launch, Limits(), ["true"]);
        var file = ProcessGroupFiles.PgidPath(pgroups, launch.Value);
        Assert.Contains(file, argv, StringComparer.Ordinal);
        var process = Process.Start(new ProcessStartInfo(setsid, ["sleep", "60"]) { UseShellExecute = false }) ?? throw new InvalidOperationException("setsid did not start");
        _started.Add(process);
        for (var i = 0; !ProcessGroupFiles.Alive(process.Id); i++)
        {
            Assert.True(i < 500, "setsid did not make its process group within 5 s");
            RealTime.Sleep(TimeSpan.FromMilliseconds(10));
        }

        ProcessGroupFiles.WritePgid(pgroups, launch.Value, process.Id);

        Assert.Equal(file, ProcessGroupFiles.PgidPath(pgroups, launch.Value), StringComparer.Ordinal);
        Assert.Equal([launch.Value], await provider.GetRequiredService<ISliceScopes>().Live(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Exec_removes_its_run_area_and_leaves_the_stored_one()
    {
        using var area = new TempRunArea();
        var stored = area.Plan;
        var sandbox = new TrackingSandbox(createSeatbeltTemp: false);
        var exec = ExecWith(sandbox, area);

        _ = await exec.Run(stored, ["true"], TestContext.Current.CancellationToken);

        var execPlan = Assert.IsType<SandboxPlan>(sandbox.Discarded);
        var execArea = RunAreaOf(execPlan);
        Assert.NotEqual(RunAreaOf(stored), execArea, StringComparer.Ordinal);
        Assert.False(Directory.Exists(execArea));
        Assert.True(Directory.Exists(RunAreaOf(stored)));
    }

    [Fact]
    public async Task Exec_removes_the_seatbelt_temp_directory_its_plan_mounts()
    {
        Assert.SkipUnless(CanMakeSeatbeltTemp(), "/private/tmp cannot be made here (Seatbelt's per-launch temp lives there on a Mac)");
        using var area = new TempRunArea();
        var stored = area.Plan with { Mounts = [.. area.Plan.Mounts, new PlanMount(SandboxPaths.SeatbeltTemp(area.Plan.Launch), PlanMountKind.ReadWrite, Reason: "test")] };
        var sandbox = new TrackingSandbox(createSeatbeltTemp: true);
        var exec = ExecWith(sandbox, area);

        _ = await exec.Run(stored, ["true"], TestContext.Current.CancellationToken);

        var temp = SandboxPaths.SeatbeltTemp(Assert.IsType<SandboxPlan>(sandbox.Discarded).Launch);
        Assert.NotEqual(SandboxPaths.SeatbeltTemp(stored.Launch), temp, StringComparer.Ordinal);
        Assert.False(Directory.Exists(temp));
    }

    [Fact]
    public void Off_macOS_and_off_linux_neither_verb_is_registered()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<ISandbox, UnavailableSandbox>()
            .AddSindriPlatformSandbox(Env, linux: false, uid: 501, mac: false)
            .BuildServiceProvider();

        Assert.Null(provider.GetService<ISandboxExec>());
        Assert.Null(provider.GetService<ISandboxLogin>());
    }

    private static bool CanMakeSeatbeltTemp()
    {
        try
        {
            _ = Directory.CreateDirectory("/private/tmp");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? RunAreaOf(SandboxPlan plan) => RunAreaPreparer.RunAreaOf(plan);

    private static ScopeLimits Limits() =>
        ScopeLimits.From(new SandboxLimits(Memory: "8GiB", Total: "20GiB"), new BoxFacts(32L << 30, 4), TimeSpan.FromMinutes(10), TimeSpan.FromHours(24), attended: false);

    private static SandboxExec ExecWith(TrackingSandbox sandbox, TempRunArea area)
    {
        var network = new SystemEgressNetwork();
        return new SandboxExec(
            sandbox, new InlineCap(), new EgressService(network, network, Environment.GetEnvironmentVariable, Path.Combine(area.Root, "egress")),
            new NoApprovedProfile(), new BoxFacts(8L << 30, 4), static (_, _, inner) => inner, () => Path.Combine(area.Root, "launches"));
    }

    // why: stands for what Wrap leaves on disk (the exec's run area, and on a Mac Seatbelt's temp directory); the fake makes it when the exec discards, so it exists even if the egress or the process refused to start
    private sealed class TrackingSandbox(bool createSeatbeltTemp) : ISandbox
    {
        public SandboxPlan? Discarded { get; private set; }

        public Result<SandboxCapabilities> Probe() => new SandboxCapabilities(SandboxMechanism.Bubblewrap);

        public Result<SandboxPlan> Plan(SandboxRequest request) => throw new NotSupportedException();

        public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => throw new NotSupportedException();

        public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => Results.Success<IReadOnlyList<string>>(["/bin/sh", "-c", "exit 0"]);

        public void Discard(SandboxPlan plan)
        {
            Discarded = plan;
            if (RunAreaPreparer.RunAreaOf(plan) is { } execArea)
            {
                _ = Directory.CreateDirectory(execArea);
            }

            if (createSeatbeltTemp)
            {
                _ = Directory.CreateDirectory(SandboxPaths.SeatbeltTemp(plan.Launch));
            }
        }
    }

    private sealed class InlineCap : ISessionCap
    {
        public Task<Result<T>> Start<T>(int cap, Func<CancellationToken, Task<Result<T>>> start, CancellationToken cancellationToken) => start(cancellationToken);
    }

    private sealed class NoApprovedProfile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<LoadedProfile?>(value: null));
    }
}
