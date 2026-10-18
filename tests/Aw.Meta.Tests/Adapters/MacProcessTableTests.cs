using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

/// <summary>MacProcessTable through a fake command runner, so it runs on any OS.</summary>
public sealed class MacProcessTableTests
{
    private const long DiskFree = 987_654_321;

    private static (MacProcessTable Table, InMemoryCommandRunner Runner) Make(Func<InMemoryCommandRunner, InMemoryCommandRunner>? script = null)
    {
        var runner = new InMemoryCommandRunner();
        _ = (script ?? (r => r.When("ps -axww", MacOutput.Ps).When("vm_stat", MacOutput.VmStat).When("vm.swapusage", MacOutput.Swap).When("kill -TERM 4242")))(runner);
        return (new MacProcessTable(new MacHostReader("/state", runner, MacToolPaths.Default, _ => DiskFree)), runner);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_tools_run_in_order_ps_then_vm_stat_then_swap()
    {
        var (table, runner) = Make();

        _ = await table.Snapshot(TestContext.Current.CancellationToken);

        Assert.Equal(["/bin/ps -axww -o pid=,ppid=,etime=,rss=,stat=,args=", "/usr/bin/vm_stat", "/usr/sbin/sysctl -n vm.swapusage"], runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_snapshot_maps_processes_without_zombies_memory_swap_and_disk()
    {
        var (table, _) = Make();

        var snapshot = await table.Snapshot(TestContext.Current.CancellationToken);

        Assert.True(snapshot.IsSuccess);
        Assert.Equal([1, 45366], snapshot.Value.Processes.Select(p => p.Pid));
        Assert.Equal(("sleep 60", 84627, 38L), (snapshot.Value.Processes[1].Cmdline, snapshot.Value.Processes[1].Ppid, snapshot.Value.Processes[1].AgeSeconds));
        Assert.Equal((1_391_046L * 16, 4_122_112L, DiskFree), (snapshot.Value.MemAvailableKb, snapshot.Value.SwapUsedKb, snapshot.Value.DiskFreeBytes));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_ps_and_vm_stat_both_fail_ps_s_error_wins_and_the_rest_do_not_run()
    {
        var (table, runner) = Make(r => r.When("ps -axww", "", exitCode: 1).When("vm_stat", "", exitCode: 1));

        var snapshot = await table.Snapshot(TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, snapshot.Error.Code);
        Assert.Contains("exited 1", snapshot.Error.Message, StringComparison.Ordinal);
        Assert.StartsWith("/bin/ps -axww", Assert.Single(runner.Lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Of_vm_stat_and_swap_failing_vm_stat_s_error_wins()
    {
        var (table, _) = Make(r => r.When("ps -axww", MacOutput.Ps).When("vm_stat", "", exitCode: 1).When("vm.swapusage", "", exitCode: 1));

        var snapshot = await table.Snapshot(TestContext.Current.CancellationToken);

        Assert.Contains("vm_stat exited 1", snapshot.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unreadable_output_is_AW_META_010_unavailable_on_macOS()
    {
        var (table, _) = Make(r => r.When("ps -axww", MacOutput.Ps).When("vm_stat", "nothing useful\n").When("vm.swapusage", MacOutput.Swap));

        var snapshot = await table.Snapshot(TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, snapshot.Error.Code);
        Assert.Contains("unavailable on macOS: vm_stat reported no page size", snapshot.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Terminate_sends_SIGTERM_through_kill()
    {
        var (table, runner) = Make();

        var done = await table.Terminate(4242, TestContext.Current.CancellationToken);

        Assert.True(done.IsSuccess);
        Assert.Equal(["/bin/kill -TERM 4242"], runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Terminate_of_a_process_that_is_not_there_is_AW_META_010()
    {
        var (table, _) = Make(r => r.When("kill -TERM 4242", "", exitCode: 1));

        var done = await table.Terminate(4242, TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, done.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-3)]
    public async Task Terminate_refuses_init_and_non_pids_without_running_anything(int pid)
    {
        var (table, runner) = Make();

        var done = await table.Terminate(pid, TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, done.Error.Code);
        Assert.Empty(runner.Lines);
    }
}
