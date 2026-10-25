using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The real scope control over a scripted systemctl and scripted cgroup files (sandbox spec §3.3, spike item 4).</summary>
[Lane(TestLane.Unit)]
public sealed class SystemdScopeTests
{
    private const string Unit = "sindri-unit.scope";
    private const string Group = "/user.slice/user-1000.slice/user@1000.service/sindri.slice/sindri-steps.slice/sindri-unit.scope";
    private readonly FakeProcesses _processes = new();
    private readonly FakeHostPaths _host = new() { RealUid = 1000 };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SystemdScope Scope() => new(Unit, _processes, _host);

    private void Running(string memoryEvents = "low 0\nhigh 0\nmax 0\noom 0\noom_kill 0\n")
    {
        _ = _processes.When((_, a) => a.Contains("show"), FakeProcesses.Ok($"ActiveState=active\nResult=success\nControlGroup={Group}\n"));
        _host.Files["/sys/fs/cgroup" + Group + "/memory.events"] = memoryEvents;
        _host.Files["/sys/fs/cgroup" + Group + "/memory.current"] = "1000\n";
        _host.Files["/sys/fs/cgroup" + Group + "/memory.peak"] = "2500\n";
        _host.Files["/sys/fs/cgroup" + Group + "/pids.current"] = "12\n";
        _host.Files["/sys/fs/cgroup" + Group + "/cpu.stat"] = "usage_usec 5000000\nuser_usec 3000000\nsystem_usec 2000000\n";
    }

    [Fact]
    public async Task Sample_reads_the_counters_of_the_scopes_cgroup()
    {
        Running("low 0\nhigh 3\nmax 1\noom 1\noom_kill 0\n");

        var sample = await Scope().Sample(Token);

        Expect.Equal(new ScopeSample(1000, 2500, new MemoryEvents(High: 3, Max: 1, Oom: 1, OomKill: 0), Tasks: 12, CpuMicroseconds: 5000000), sample);
    }

    [Fact]
    public async Task Sample_looks_the_control_group_up_once()
    {
        Running();
        var scope = Scope();

        _ = await scope.Sample(Token);
        _ = await scope.Sample(Token);

        Expect.Equal(1, _processes.Calls.Count(c => c.Arguments.Contains("show")));
    }

    [Fact]
    public async Task A_scope_whose_files_are_gone_samples_nothing()
    {
        Running();
        var scope = Scope();
        _ = await scope.Sample(Token);
        _host.Files.Clear();   // the cgroup is removed with the scope

        Expect.Equal(null, await scope.Sample(Token));
    }

    [Fact]
    public async Task A_unit_systemd_does_not_know_samples_nothing()
    {
        _ = _processes.When((_, a) => a.Contains("show"), FakeProcesses.Ok("ActiveState=inactive\nResult=success\nControlGroup=\n"));

        Expect.Equal(null, await Scope().Sample(Token));
    }

    [Fact]
    public async Task The_result_of_a_failed_scope_names_the_oom_kill()
    {
        _ = _processes.When((_, a) => a.Contains("show"), FakeProcesses.Ok("ActiveState=failed\nResult=oom-kill\nControlGroup=\n"));

        Expect.Equal("oom-kill", await Scope().Result(Token));
    }

    [Fact]
    public async Task A_running_scope_has_no_result_yet()
    {
        Running();

        Expect.Equal(null, await Scope().Result(Token));
    }

    [Fact]
    public async Task Kill_and_the_ceiling_go_through_systemctl()
    {
        _ = _processes.When((_, _) => true, FakeProcesses.Ok());
        var scope = Scope();

        await scope.Kill(force: true, Token);
        await scope.SetRuntimeMax(TimeSpan.FromSeconds(90), Token);

        Expect.Equal(("systemctl", string.Join(' ', SystemdScopeArgv.Kill(Unit, force: true))), (_processes.Calls[0].Program, string.Join(' ', _processes.Calls[0].Arguments)));
        Expect.Equal(("systemctl", string.Join(' ', SystemdScopeArgv.SetRuntimeMax(Unit, TimeSpan.FromSeconds(90)))), (_processes.Calls[1].Program, string.Join(' ', _processes.Calls[1].Arguments)));
    }

    [Fact]
    public async Task A_systemctl_that_cannot_run_is_no_error()
    {
        var scope = Scope();   // every call answers "not started"

        await scope.Kill(force: false, Token);
        await scope.SetRuntimeMax(TimeSpan.FromSeconds(90), Token);

        Expect.Equal(2, _processes.Calls.Count);
    }
}
