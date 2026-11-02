using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The watchdog over a real systemd user scope and the real cgroup files (sandbox spec §3.3, §15 item 4). Skips where the box cannot run a user scope with a memory limit.</summary>
[Lane(TestLane.Heavy)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LimitWatchdogSystemdTests : IDisposable
{
    private const long Mib = 1024 * 1024;
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(90);
    private static readonly string? _unavailable = Detect();
    private readonly SandboxProcesses _processes = new();
    private readonly string _unit = "sindri-test-" + RealTime.NewGuid().ToString("N") + ".scope";

    public void Dispose()
    {
        // the test owns this unit: stop it and clear its failed state, nothing else
        _ = _processes.Run("systemctl", ["--user", "stop", _unit], TimeSpan.FromSeconds(10));
        _ = _processes.Run("systemctl", ["--user", "reset-failed", _unit], TimeSpan.FromSeconds(10));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string? Detect()
    {
        var sandbox = new SandboxProcesses();
        if (!OperatingSystem.IsLinux())
        {
            return "systemd scopes are Linux only";
        }

        var state = sandbox.Run("systemctl", ["--user", "is-system-running"], TimeSpan.FromSeconds(10));
        if (!state.Started || state.Output.Trim() is not ("running" or "degraded"))
        {
            return "no systemd user manager here (systemctl --user is-system-running: " + state.Output.Trim() + ")";
        }

        if (!sandbox.Run("systemd-run", ["--version"], TimeSpan.FromSeconds(10)).Succeeded)
        {
            return "systemd-run is missing";
        }

        if (!sandbox.Run("python3", ["--version"], TimeSpan.FromSeconds(10)).Succeeded)
        {
            return "python3 is missing";
        }

        var uid = CgroupControllers.UidFrom(File.ReadAllText("/proc/self/status"));
        string? controllers = null;
        try
        {
            controllers = uid is { } id ? File.ReadAllText(CgroupControllers.FileFor(id)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // unreadable: nothing is claimed
        }

        return CgroupControllers.FirstMissing(controllers) is { } missing ? "the user manager has no delegated " + missing + " controller" : null;
    }

    private Task<int> InScope(IReadOnlyList<string> properties, IReadOnlyList<string> command) =>
        Task.Run(
            () =>
            {
                string[] argv = ["--user", "--scope", "--quiet", "--unit=" + _unit, .. properties.SelectMany(p => new[] { "-p", p }), "--", .. command];
                return _processes.Run("systemd-run", argv, _deadline).ExitCode;
            },
            Token);

    private Task<LimitEnd?> Watch(Task exited, long? wall) =>
        new LimitWatchdog(new SystemctlScopeControl(_processes), new CgroupFiles(), new FakeWrapCalls(TimeProvider.System), TimeProvider.System, new WrapLimits(_unit, 5, 3600, 64 * Mib, wall))
            .Run(A.FirstLaunchId, new LaunchSecret("proof"), exited, Token);

    [Fact]
    public async Task A_memory_hog_ends_limit_memory()
    {
        Assert.SkipUnless(_unavailable is null, _unavailable ?? string.Empty);
        const string Hog = "l = []\nwhile True:\n    l.append(b'x' * (1 << 20))\n";
        var child = InScope(["MemoryMax=64M", "MemorySwapMax=0", "OOMPolicy=kill"], ["python3", "-c", Hog]);

        var end = await Watch(child, wall: 120).WaitAsync(_deadline, TimeProvider.System, Token);

        Expect.Equal(LimitKind.Memory, end!.Kind);
        Expect.Equal(64 * Mib, end.Limit);
    }

    [Fact]
    public async Task A_sleeper_ends_limit_wall()
    {
        Assert.SkipUnless(_unavailable is null, _unavailable ?? string.Empty);
        var child = InScope(["MemoryMax=64M"], ["sleep", "300"]);

        var end = await Watch(child, wall: 3).WaitAsync(_deadline, TimeProvider.System, Token);

        Expect.Equal(LimitKind.Wall, end!.Kind);
        Expect.Equal(3, end.Limit);
        Expect.True(end.Peak >= 3);
    }
}
