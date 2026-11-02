using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;

namespace Aw.Sindri.Tests.Sandbox.Seatbelt;

/// <summary>Cancelling a Mac exec (#1945): the command's group must not outlive its group file, or the session cap stops counting it and the watchdog can no longer signal it.</summary>
[Lane(TestLane.Integration)]
public sealed class SeatbeltExecCancelTests : IDisposable
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(30);
    private readonly string _directory = Directory.CreateTempSubdirectory("aw-exec-cancel-").FullName;
    private int? _pgid;

    public void Dispose()
    {
        if (_pgid is { } pgid && ProcessGroupFiles.Alive(pgid))
        {
            _ = ProcessGroupFiles.Signal(pgid, "SIGKILL");
        }

        Directory.Delete(_directory, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_cancelled_exec_leaves_no_live_group_without_its_file()
    {
        Assert.SkipUnless(File.Exists("/usr/bin/perl") && File.Exists("/bin/sh"), "needs /usr/bin/perl and /bin/sh");
        Assert.SkipUnless(new SandboxProcesses().Run("socat", ["-V"], TimeSpan.FromSeconds(10)).Succeeded, "socat is not installed (the exec's egress needs it)");
        using var area = new TempRunArea();
        var launches = Path.Combine(area.Root, "launches");
        var pgroups = ProcessGroupFiles.Directory(launches);
        var exec = Exec(area, launches, pgroups);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = exec.Run(area.Plan, ["/bin/sh", "-c", "sleep 60"], cts.Token);
        var file = await WaitFor(() => Directory.Exists(pgroups) ? Directory.GetFiles(pgroups, "*.pgid").SingleOrDefault() : null, _wait);

        if (file is null)
        {
            Assert.Fail(run.IsCompleted ? "the exec ended before it wrote a .pgid: " + (await run).Error : "no .pgid file appeared");
        }

        _pgid = ProcessGroupFiles.ReadPgid(pgroups, Path.GetFileNameWithoutExtension(file));
        var pgid = Assert.NotNull(_pgid);
        Assert.True(ProcessGroupFiles.Alive(pgid), "the group is not alive before the cancel");

        await cts.CancelAsync();
        try
        {
            _ = await run.WaitAsync(_wait, Token);
        }
        catch (OperationCanceledException)
        {
            // a cancelled run may end by throwing; the group and its file are what this test checks
        }

        _ = await WaitFor(() => ProcessGroupFiles.Alive(pgid) ? null : "gone", TimeSpan.FromSeconds(5));

        Assert.True(!ProcessGroupFiles.Alive(pgid) || File.Exists(file), "a live exec group lost its .pgid file: uncounted and unsignalable");
    }

    private static SandboxExec Exec(TempRunArea area, string launches, string pgroups)
    {
        var network = new SystemEgressNetwork();
        return new SandboxExec(
            new PassThroughSandbox(), new InlineCap(), new EgressService(network, network, Environment.GetEnvironmentVariable, Path.Combine(area.Root, "egress")),
            new NoApprovedProfile(), new BoxFacts(8L << 30, 4), new ExecPlatform(ExecScope.Mac(pgroups), ExecScope.MacEnded(pgroups)), () => launches);
    }

    private static async Task<string?> WaitFor(Func<string?> probe, TimeSpan limit)
    {
        var deadline = RealTime.NowDateTime + limit;
        while (true)
        {
            if (probe() is { } found)
            {
                return found;
            }

            if (RealTime.NowDateTime >= deadline)
            {
                return null;
            }

            await Task.Delay(50, Token);
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

    // why: passes the command through unchanged so the real Mac exec scope's group wrapper runs it
    private sealed class PassThroughSandbox : ISandbox
    {
        public Result<SandboxCapabilities> Probe() => new SandboxCapabilities(SandboxMechanism.Bubblewrap);

        public Result<SandboxPlan> Plan(SandboxRequest request) => throw new NotSupportedException();

        public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => throw new NotSupportedException();

        public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => Results.Success(argv);

        public void Discard(SandboxPlan plan)
        {
        }
    }
}
