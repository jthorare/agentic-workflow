using System.Diagnostics;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Seatbelt;

/// <summary>The Mac exec scope as a real child (#1936): sandbox exec starts it as a plain child of sindri, so the scope itself must make the command its own process group, and the group file must name a live group.</summary>
[Lane(TestLane.Integration)]
public sealed class SeatbeltExecScopeProcessTests : IDisposable
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(30);
    private readonly string _directory = Directory.CreateTempSubdirectory("aw-exec-scope-").FullName;
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

        Directory.Delete(_directory, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_command_runs_in_its_own_live_group_and_a_piped_stdin_is_not_stopped()
    {
        Assert.SkipUnless(File.Exists("/usr/bin/perl") && File.Exists("/bin/sh"), "needs /usr/bin/perl and /bin/sh");
        var pgroups = ProcessGroupFiles.Directory(_directory);
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        var argv = ExecScope.Mac(pgroups)(launch, Limits(), ["/bin/sh", "-c", "echo ran; read x; echo got:$x"]);

        var process = Process.Start(new ProcessStartInfo(argv[0], argv.Skip(1)) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new InvalidOperationException("did not start");
        _started.Add(process);
        var first = await process.StandardOutput.ReadLineAsync(Token).AsTask().WaitAsync(_wait, Token);

        Assert.Equal("ran", first);   // the start was not refused (exit 70) for sh not being a group leader
        var pgid = ProcessGroupFiles.ReadPgid(pgroups, launch.Value);
        _ = Assert.NotNull(pgid);
        Assert.NotEqual(Libc.OwnGroup(), pgid);
        Assert.True(ProcessGroupFiles.Alive(pgid.Value), "the group file names a group that is not alive");
        await process.StandardInput.WriteLineAsync("hi".AsMemory(), Token);
        process.StandardInput.Close();
        Assert.Equal("got:hi", await process.StandardOutput.ReadLineAsync(Token).AsTask().WaitAsync(_wait, Token));
        await process.WaitForExitAsync(Token).WaitAsync(_wait, Token);
        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public async Task Mac_exec_scope_without_a_tty_still_writes_a_live_group()
    {
        Assert.SkipUnless(File.Exists("/usr/bin/perl") && File.Exists("/bin/sh"), "needs /usr/bin/perl and /bin/sh");
        var pgroups = ProcessGroupFiles.Directory(_directory);
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "2");
        var argv = ExecScope.Mac(pgroups)(launch, Limits(), ["/bin/sh", "-c", "echo ran; read x; exit 0"]);
        // why: setsid (where it exists) drops the controlling terminal, so /dev/tty cannot open for the child; elsewhere (macOS has no setsid) a test host launched from an agent or CI has none, and stdin and stdout are pipes either way
        var setsid = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(d => Path.Combine(d, "setsid")).FirstOrDefault(File.Exists);
        var start = setsid is null ? argv : ["setsid", .. argv];

        var process = Process.Start(new ProcessStartInfo(start[0], start.Skip(1)) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new InvalidOperationException("did not start");
        _started.Add(process);

        Assert.Equal("ran", await process.StandardOutput.ReadLineAsync(Token).AsTask().WaitAsync(_wait, Token));
        var pgid = ProcessGroupFiles.ReadPgid(pgroups, launch.Value);
        _ = Assert.NotNull(pgid);
        Assert.True(ProcessGroupFiles.Alive(pgid.Value), "the group file names a group that is not alive");
        process.StandardInput.Close();
        await process.WaitForExitAsync(Token).WaitAsync(_wait, Token);
        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public async Task Exec_removes_the_group_file_of_its_launch_when_it_ends()
    {
        var home = Path.Combine(_directory, "home");
        var launcher = Path.Combine(_directory, "launcher");
        var pgroups = ProcessGroupFiles.Directory(new LauncherOptions(launcher).LaunchDirectory);   // why: the folder the registered exec and a Step launch both use
        using var area = new TempRunArea();
        var sandbox = new SeatbeltVerbsEdgeTests.TrackingSandbox(createSeatbeltTemp: false, onDiscard: plan =>
        {
            // why: stands for the file the scope's script wrote for this exec's launch id, which the test cannot know before the exec picks it
            _ = Directory.CreateDirectory(pgroups);
            File.WriteAllText(ProcessGroupFiles.PgidPath(pgroups, plan.Launch.Value)!, "99999\n");
        });
        await using var provider = new ServiceCollection()
            .AddSingleton<ISandbox, UnavailableSandbox>()
            .AddSingleton(new SindriSettings(home, Path.Combine(home, "profile.yaml")))
            .AddSingleton(new LauncherOptions(launcher))
            .AddSingleton<TimeProvider>(new FakeTimeProvider())
            .AddSingleton<ISandboxProcesses, SandboxProcesses>()
            .AddSingleton<ISandboxMachine>(new Machine())
            .AddSingleton<IApprovedProfile, NoApprovedProfile>()
            .AddSingleton<ILaunchSecrets>(_ => throw new NotSupportedException("not needed to resolve the exec"))
            .AddSindriPlatformSandbox(static _ => null, linux: false, uid: 501, mac: true)
            .AddSingleton<ISandbox>(sandbox)
            .BuildServiceProvider();

        _ = await provider.GetRequiredService<ISandboxExec>().Run(area.Plan, ["true"], Token);

        Assert.NotNull(sandbox.Discarded);
        Assert.Empty(Directory.GetFiles(pgroups, "*.pgid"));
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
}
