using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>'session wrap --limits' (sandbox spec §3.3; SB-3a): a sandboxed agent never runs unwatched, and a limit that ended it reaches the control plane.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionWrapLimitsTests : IDisposable
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(20);
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeChildren _children = new();
    private readonly FakeWrapCalls _calls;
    private readonly FakeScopeControl _scope;
    private readonly FakeCgroupStats _stats = new();
    private readonly StringWriter _errors = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-wrap-limits-").FullName;

    public SessionWrapLimitsTests()
    {
        _calls = new FakeWrapCalls(_clock);
        _scope = new FakeScopeControl(_clock);
    }

    public void Dispose()
    {
        _errors.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] Args(string limitsFile) => ["--launch", A.FirstLaunchId.Value, "--limits", limitsFile, "--", "/bin/sh", "-c", "agent"];

    private string LimitsFile(string unit)
    {
        var path = Path.Combine(_dir, "limits.json");
        File.WriteAllText(path, $$"""{"unit":"{{unit}}","wallGraceSeconds":5,"parkMaxSeconds":3600,"memoryMaxBytes":67108864,"wallSeconds":600}""");
        return path;
    }

    private SessionWrapCommand Command(WatchdogPorts? ports = null)
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin:/bin", ["AW_SINDRI_LAUNCH_SECRET"] = "wrap-proof" };
        return new SessionWrapCommand(_calls, _children, name => env.GetValueOrDefault(name), _clock, _errors, new FakeIds(), new WrapExtras(Watchdog: ports ?? new WatchdogPorts(_scope, _stats)));
    }

    [Fact]
    public async Task Limits_file_that_cannot_be_read_refuses_the_start_with_exit_2()
    {
        var code = await Command().Run(Args(Path.Combine(_dir, "no-such.json")), Token);

        Expect.Equal(2, code);
        Expect.True(_errors.ToString().Contains("cannot be read", StringComparison.Ordinal));
        Expect.True(_children.Child is null && _calls.Exits.Count == 0);
    }

    [Fact]
    public async Task Limits_file_without_a_unit_refuses_the_start()
    {
        var code = await Command().Run(Args(LimitsFile(unit: " ")), Token);

        Expect.Equal(2, code);
        Expect.True(_errors.ToString().Contains("names no unit", StringComparison.Ordinal));
        Expect.True(_children.Child is null && _calls.Exits.Count == 0);
    }

    [Fact]
    public async Task A_limit_exit_sends_the_limit_to_the_control_plane()
    {
        _stats.Sample = new CgroupSample(OomKills: 1, MemoryCurrent: 60_000_000, MemoryPeak: 67_000_000, Pids: 4, CpuMicroseconds: 0);
        var run = Command().Run(Args(LimitsFile("sindri-LCH-1.scope")), Token);
        await Until(() => _children.Child is not null && _stats.Reads > 0);

        _children.Child!.Exit(137);
        var code = await run.WaitAsync(_deadline, TimeProvider.System, Token);

        Expect.Equal(137, code);
        var limit = Expect.Only(_calls.Limits);
        Expect.Equal(LimitKind.Memory, limit!.Kind);
        Expect.Equal(67_000_000, limit.Peak);
    }

    [Fact]
    public async Task An_ordinary_exit_under_limits_sends_no_limit()
    {
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 1, MemoryPeak: 1, Pids: 1, CpuMicroseconds: 0);
        var run = Command().Run(Args(LimitsFile("sindri-LCH-1.scope")), Token);
        await Until(() => _children.Child is not null && _stats.Reads > 0);

        _children.Child!.Exit(0);
        _ = await run.WaitAsync(_deadline, TimeProvider.System, Token);

        Expect.True(Expect.Only(_calls.Limits) is null);
    }

    [Fact]
    public async Task In_process_group_mode_the_group_file_is_removed_once_the_child_has_exited()
    {
        var pgroups = Path.Combine(_dir, "pgroups");
        ProcessGroupFiles.WritePgid(pgroups, "sindri-LCH-1.scope", 2_000_000_000);   // why: far above any pid_max, and not this process's group
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 1, MemoryPeak: 1, Pids: 1, CpuMicroseconds: 0);
        var ports = new WatchdogPorts(_scope, _stats, new NoKills(), unit => ProcessGroupFiles.Remove(pgroups, unit));
        var run = Command(ports).Run(Args(LimitsFile("sindri-LCH-1.scope")), Token);
        await Until(() => _children.Child is not null && _stats.Reads > 0);
        Expect.Equal(2_000_000_000, ProcessGroupFiles.ReadPgid(pgroups, "sindri-LCH-1.scope"));

        _children.Child!.Exit(0);
        _ = await run.WaitAsync(_deadline, TimeProvider.System, Token);

        Expect.True(ProcessGroupFiles.ReadPgid(pgroups, "sindri-LCH-1.scope") is null);
    }

    [Fact]
    public async Task Without_process_group_mode_the_group_file_is_left_alone()
    {
        var pgroups = Path.Combine(_dir, "pgroups");
        ProcessGroupFiles.WritePgid(pgroups, "sindri-LCH-1.scope", 2_000_000_000);
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 1, MemoryPeak: 1, Pids: 1, CpuMicroseconds: 0);
        var run = Command().Run(Args(LimitsFile("sindri-LCH-1.scope")), Token);
        await Until(() => _children.Child is not null && _stats.Reads > 0);

        _children.Child!.Exit(0);
        _ = await run.WaitAsync(_deadline, TimeProvider.System, Token);

        Expect.Equal(2_000_000_000, ProcessGroupFiles.ReadPgid(pgroups, "sindri-LCH-1.scope"));
    }

    [Fact]
    public async Task Limits_without_a_watchdog_refuse_the_start()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin:/bin", ["AW_SINDRI_LAUNCH_SECRET"] = "wrap-proof" };
        var bare = new SessionWrapCommand(_calls, _children, name => env.GetValueOrDefault(name), _clock, _errors, new FakeIds());

        var code = await bare.Run(Args(LimitsFile("sindri-LCH-1.scope")), Token);

        Expect.Equal(2, code);
        Expect.True(_children.Child is null);
    }

    private static async Task Until(Func<bool> condition)
    {
        var limit = TimeProvider.System.GetTimestamp();
        while (!condition())
        {
            Assert.True(TimeProvider.System.GetElapsedTime(limit) < _deadline, "timed out waiting for the wrap");
            await Task.Delay(2, Token);
        }
    }

    private sealed class NoKills : IMemoryKills
    {
        public Task<bool> Record(string unit, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
