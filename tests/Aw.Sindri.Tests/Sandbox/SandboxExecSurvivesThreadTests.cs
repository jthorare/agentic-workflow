using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>
/// #1684: 'sandbox exec' runs 'systemd-run … bwrap --die-with-parent', and PR_SET_PDEATHSIG fires when the thread that forked exits. In the CLI that is a pool thread
/// that retires after about 20 s idle, so the command was killed (exit 137). The real exec path, a real bubblewrap and a real user scope, started from a thread that
/// exits at once.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxExecSurvivesThreadTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task Exec_command_survives_the_thread_that_started_it()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the bubblewrap sandbox is Linux-only");
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        var processes = new SandboxProcesses();
        Assert.SkipUnless(processes.Run("socat", ["-V"], TimeSpan.FromSeconds(10)).Succeeded, "socat is not installed (the exec's egress needs it)");
        var scope = processes.Run("systemd-run", ["--user", "--scope", "--quiet", "true"], TimeSpan.FromSeconds(15));
        Assert.SkipUnless(scope.Succeeded, "systemd-run --user cannot make a scope here: " + scope.Error.Trim());
        using var area = new TempRunArea();
        var network = new SystemEgressNetwork();
        var exec = new SandboxExec(
            BubblewrapBox.Create(), new InlineCap(), new EgressService(network, network, Environment.GetEnvironmentVariable, Path.Combine(area.Root, "egress")),
            new NoApprovedProfile(), new BoxFacts(8L << 30, 4), "systemd-run", () => Path.Combine(area.Root, "launches"));

        // Everything before Process.Start completes synchronously (InlineCap, NoApprovedProfile), so Run forks on this thread and returns the pending wait.
        Task<Result<int>>? running = null;
        var starter = new Thread(() => running = exec.Run(area.Plan, ["sh", "-c", "sleep 2; exit 0"], CancellationToken.None));
        starter.Start();
        starter.Join();   // the forking thread is gone before the command can finish

        var result = await running!.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(0, result.Value);
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
