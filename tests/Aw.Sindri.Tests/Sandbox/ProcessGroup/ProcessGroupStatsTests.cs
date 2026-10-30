using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;

namespace Aw.Sindri.Tests.Sandbox.ProcessGroup;

/// <summary>A process group's accounting from one 'ps' listing (SB-9b), with the output of both macOS and procps.</summary>
[Lane(TestLane.Unit)]
public sealed class ProcessGroupStatsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-pgstats-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("00:00:01", 1_000_000L)]
    [InlineData("0:00.12", 120_000L)]
    [InlineData("1:02.50", 62_500_000L)]
    [InlineData("01:02:03", 3_723_000_000L)]
    [InlineData("2-03:04:05", 183_845_000_000L)]
    [InlineData("1-00:00:00.5", 86_400_500_000L)]
    public void A_ps_time_becomes_microseconds(string text, long microseconds) =>
        Assert.Equal(microseconds, ProcessGroupStats.CpuMicroseconds(text));

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("a:b")]
    [InlineData("x-01:00:00")]
    [InlineData("1:2:3:4")]
    public void Anything_else_is_not_a_time(string text) => Assert.Null(ProcessGroupStats.CpuMicroseconds(text));

    [Fact]
    public void Read_sums_the_processes_of_the_group_and_leaves_the_others()
    {
        var stats = Stats("  100   500   2048  0:01.00\n  101   500   1024  0:00.50\n  200   900   99999  5:00.00\n", out var processes);

        var sample = stats.Read("pgid:500");

        Assert.NotNull(sample);
        Assert.Equal(3 * 1024 * 1024L, sample.MemoryCurrent);
        Assert.Equal(2, sample.Pids);
        Assert.Equal(1_500_000L, sample.CpuMicroseconds);
        Assert.Equal(0, sample.OomKills);
        var (program, arguments) = Assert.Single(processes.Calls);
        Assert.Equal("ps", program);
        Assert.Equal(["-A", "-o", "pid=,pgid=,rss=,time="], arguments);
    }

    [Fact]
    public void A_group_with_no_process_is_gone()
    {
        var stats = Stats("  100   500   2048  0:01.00\n", out _);

        Assert.Null(stats.Read("pgid:501"));
    }

    [Fact]
    public void A_handle_that_is_not_a_group_or_a_ps_that_fails_reads_nothing()
    {
        var failing = new ProcessGroupStats(new FakeProcesses().When((_, _) => true, FakeProcesses.Failed("boom")), _dir);

        Assert.Null(Stats("  100   500   2048  0:01.00\n", out _).Read("/user.slice/x.scope"));
        Assert.Null(failing.Read("pgid:500"));
    }

    [Fact]
    public void The_peak_is_the_highest_figure_seen_for_the_group()
    {
        var ps = new ScriptedPs(["  100   500   4096  0:01.00\n", "  100   500   1024  0:02.00\n"]);
        var stats = new ProcessGroupStats(ps, _dir);

        var first = stats.Read("pgid:500");
        var second = stats.Read("pgid:500");

        Assert.Equal(4096 * 1024L, first?.MemoryPeak);
        Assert.Equal(1024 * 1024L, second?.MemoryCurrent);
        Assert.Equal(4096 * 1024L, second?.MemoryPeak);
    }

    [Fact]
    public void A_memory_kill_record_shows_as_an_oom_kill()
    {
        ProcessGroupFiles.RecordMemoryKill(_dir, 500);

        var sample = Stats("  100   500   2048  0:01.00\n", out _).Read("pgid:500");

        Assert.Equal(1, sample?.OomKills);
    }

    private ProcessGroupStats Stats(string psOutput, out FakeProcesses processes)
    {
        processes = new FakeProcesses().When((_, _) => true, FakeProcesses.Ok(psOutput));
        return new ProcessGroupStats(processes, _dir);
    }

    private sealed class ScriptedPs(IEnumerable<string> outputs) : ISandboxProcesses
    {
        private readonly Queue<string> _outputs = new(outputs);

        public ProcessResult Run(string program, IReadOnlyList<string> arguments, TimeSpan timeout) => FakeProcesses.Ok(_outputs.Dequeue());
    }
}
