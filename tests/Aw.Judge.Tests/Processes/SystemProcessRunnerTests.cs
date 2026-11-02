using System.Diagnostics;
using System.Globalization;
using Aw.Judge.Core.Processes;
using Aw.Judge.Infrastructure.Processes;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Processes;

[Lane(TestLane.Integration)]
public sealed class SystemProcessRunnerTests : IDisposable
{
    private readonly ShellPrograms _bin = new();

    public void Dispose() => _bin.Dispose();

    [Fact]
    public async Task A_timeout_stops_the_processes_the_program_started_too()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        var pidFile = Path.Combine(_bin.Directory, "child.pid");
        _ = _bin.InstallScript("spawner", $"sleep 30 &\necho $! > {ShellPrograms.Quote(pidFile)}\nwait\n");
        var clock = Stopwatch.StartNew();

        var result = await new SystemProcessRunner(_bin.Environment).Run(
            new ProcessRequest { FileName = "spawner", Timeout = TimeSpan.FromSeconds(3) }, TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), $"stopped near the 3 s timeout, took {clock.Elapsed}");
        var child = int.Parse((await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken)).Trim(), CultureInfo.InvariantCulture);
        Assert.True(await Gone(child), $"the background child {child.ToString(CultureInfo.InvariantCulture)} was stopped too");
    }

    [Fact]
    public async Task A_program_that_exits_while_a_background_child_holds_its_pipes_is_exited_with_its_output()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        _ = _bin.InstallScript("daemonizer", "sleep 30 &\nprintf ok\nexit 0\n");
        var clock = Stopwatch.StartNew();

        var result = await new SystemProcessRunner(_bin.Environment).Run(
            new ProcessRequest { FileName = "daemonizer", Timeout = TimeSpan.FromSeconds(20) }, TestContext.Current.CancellationToken);

        Assert.Equal(ProcessResult.Exited(0, "ok", ""), result);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), $"returned soon after the program exited, not at the 20 s budget, took {clock.Elapsed}");
    }

    [Fact]
    public async Task Inherit_only_keeps_the_named_variables_and_the_requests_own()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        const string Kept = "AW_JUDGE_KEPT_PROBE";
        const string Dropped = "AW_JUDGE_DROPPED_PROBE";
        _ = _bin.InstallScript("printer", $"printf '[%s][%s][%s]' \"${Kept}\" \"${Dropped}\" \"$AW_JUDGE_OWN_PROBE\"\n");
        Environment.SetEnvironmentVariable(Kept, "kept");
        Environment.SetEnvironmentVariable(Dropped, "leaked");
        try
        {
            var result = await new SystemProcessRunner(_bin.Environment).Run(
                new ProcessRequest
                {
                    FileName = "printer",
                    InheritOnly = [Kept, "AW_JUDGE_ABSENT_PROBE"],
                    Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_OWN_PROBE"] = "own" },
                    Timeout = TimeSpan.FromSeconds(10),
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(ProcessResult.Exited(0, "[kept][][own]", ""), result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Kept, value: null);
            Environment.SetEnvironmentVariable(Dropped, value: null);
        }
    }

    [Fact]
    public async Task An_unset_variable_is_not_inherited()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        const string Variable = "AW_JUDGE_UNSET_PROBE";
        _ = _bin.InstallScript("printer", $"printf '[%s]' \"${Variable}\"\n");
        Environment.SetEnvironmentVariable(Variable, "inherited");
        try
        {
            var result = await new SystemProcessRunner(_bin.Environment).Run(
                new ProcessRequest { FileName = "printer", Unset = [Variable], Timeout = TimeSpan.FromSeconds(10) }, TestContext.Current.CancellationToken);

            Assert.Equal(ProcessResult.Exited(0, "[]", ""), result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, value: null);
        }
    }

    [Fact]
    public async Task A_path_is_run_as_is_without_a_search()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        var file = _bin.InstallScript("direct", "printf ok\n");

        var result = await new SystemProcessRunner(_ => null).Run(
            new ProcessRequest { FileName = file, Timeout = TimeSpan.FromSeconds(10) }, TestContext.Current.CancellationToken);

        Assert.Equal(ProcessResult.Exited(0, "ok", ""), result);
    }

    [Fact]
    public async Task An_argument_list_past_the_system_limit_failed_to_start_rather_than_not_installed()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        _ = _bin.InstallScript("echoer", "printf ok\n");

        var result = await new SystemProcessRunner(_bin.Environment).Run(
            new ProcessRequest { FileName = "echoer", Arguments = [new string('x', 4 << 20)], Timeout = TimeSpan.FromSeconds(10) },
            TestContext.Current.CancellationToken);

        Assert.Equal(ProcessEnd.FailedToStart, result.End);
        Assert.False(string.IsNullOrEmpty(result.StandardError), "the system's reason is kept");
    }

    [Fact]
    public async Task Cancelling_stops_the_program_and_throws()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        _ = _bin.InstallScript("sleeper", "sleep 30\n");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new SystemProcessRunner(_bin.Environment).Run(new ProcessRequest { FileName = "sleeper", Timeout = TimeSpan.FromSeconds(30) }, cancel.Token));
    }

    private static async Task<bool> Gone(int pid)
    {
        for (var i = 0; i < 40; i++)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return false;
    }
}
