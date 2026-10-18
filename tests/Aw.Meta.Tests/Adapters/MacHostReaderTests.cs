using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

/// <summary>The macOS reader runs every tool by the absolute path macOS ships it at (a PATH nobody controls can't substitute another program).</summary>
public sealed class MacHostReaderTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Every_tool_runs_from_its_absolute_path_when_that_file_exists()
    {
        var runner = new InMemoryCommandRunner()
            .When("hw.memsize", MacOutput.MemSize).When("vm_stat", MacOutput.VmStat).When("vm.swapusage", MacOutput.Swap)
            .When("-axww", MacOutput.Ps).When("etime=", "01:00\n").When("-TERM", string.Empty);
        var reader = new MacHostReader("/state", runner, MacToolPaths.Default, _ => 1);
        var token = TestContext.Current.CancellationToken;

        _ = await reader.TotalMemoryKb(token);
        _ = await reader.Memory(token);
        _ = await reader.Processes(token);
        _ = await reader.ProcessAge(7, token);
        _ = await reader.Terminate(7, token);

        Assert.Equal(
            [
                "/usr/sbin/sysctl -n hw.memsize",
                "/usr/bin/vm_stat",
                "/usr/sbin/sysctl -n vm.swapusage",
                "/bin/ps -axww -o pid=,ppid=,etime=,rss=,stat=,args=",
                "/bin/ps -o etime= -p 7",
                "/bin/kill -TERM 7",
            ],
            runner.Lines);
    }
}
