using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.ProcessGroup;

/// <summary>The unit files, the group signal and the live-unit count over real process groups (SB-9b). A group is made with setsid, which makes the child its own group leader.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessGroupScopeTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-pgscope-").FullName;
    private readonly List<Process> _started = [];

    public void Dispose()
    {
        foreach (var process in _started.Where(p => !p.HasExited))
        {
            process.Kill();
        }

        foreach (var process in _started)
        {
            process.Dispose();
        }

        Directory.Delete(_dir, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_live_group_is_its_unit_handle_and_a_missing_file_is_not()
    {
        var session = Session("sindri-a");
        var control = new ProcessGroupScopeControl(_dir);

        Assert.Equal("pgid:" + session.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), await control.ControlGroup("sindri-a", Token));
        Assert.Null(await control.ControlGroup("sindri-none", Token));
    }

    [Fact]
    public async Task Kill_signals_the_whole_group_and_the_unit_is_gone_afterwards()
    {
        var session = Session("sindri-a");
        var control = new ProcessGroupScopeControl(_dir);

        Assert.True(await control.Kill("sindri-a", "SIGKILL", Token));
        await session.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token);

        Assert.Null(await control.ControlGroup("sindri-a", Token));
        Assert.False(await control.Kill("sindri-a", "SIGKILL", Token));
    }

    [Fact]
    public async Task An_unknown_signal_name_sends_nothing()
    {
        var session = Session("sindri-a");

        Assert.False(await new ProcessGroupScopeControl(_dir).Kill("sindri-a", "SIGBOGUS", Token));
        Assert.False(session.HasExited);
    }

    [Fact]
    public async Task Result_is_oom_kill_only_after_the_watchdog_recorded_a_memory_kill()
    {
        _ = Session("sindri-a");
        var control = new ProcessGroupScopeControl(_dir);
        Assert.Null(await control.Result("sindri-a", Token));

        Assert.True(await control.Record("sindri-a", Token));

        Assert.Equal("oom-kill", await control.Result("sindri-a", Token));
        Assert.False(await control.Record("sindri-none", Token));
    }

    [Fact]
    public async Task A_new_group_id_for_a_unit_clears_the_record_of_a_group_with_that_id()
    {
        ProcessGroupFiles.RecordMemoryKill(_dir, 4242);

        ProcessGroupFiles.WritePgid(_dir, "sindri-b", 4242);

        Assert.Null(await new ProcessGroupScopeControl(_dir).Result("sindri-b", Token));
    }

    [Fact]
    public async Task SetRuntimeMax_does_nothing_and_says_yes() =>
        Assert.True(await new ProcessGroupScopeControl(_dir).SetRuntimeMax("sindri-a", TimeSpan.FromMinutes(1), Token));

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void A_group_id_file_that_would_signal_everyone_or_nobody_reads_as_none(string text)
    {
        File.WriteAllText(Path.Combine(_dir, "sindri-x.pgid"), text);

        Assert.Null(ProcessGroupFiles.ReadPgid(_dir, "sindri-x"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("")]
    public void A_unit_that_is_not_a_file_name_has_no_group(string unit) => Assert.Null(ProcessGroupFiles.ReadPgid(_dir, unit));

    [Fact]
    public async Task Live_lists_the_units_whose_group_still_has_a_process()
    {
        _ = Session("sindri-b");
        var dying = Session("sindri-a");
        ProcessGroupFiles.WritePgid(_dir, "sindri-gone", 2_000_000_000);   // why: far above any pid_max
        var slice = new ProcessGroupSliceScopes(_dir);
        Assert.Equal(["sindri-a", "sindri-b"], await slice.Live(Token));

        Assert.True(await new ProcessGroupScopeControl(_dir).Kill("sindri-a", "SIGKILL", Token));
        await dying.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token);

        Assert.Equal(["sindri-b"], await slice.Live(Token));
    }

    [Fact]
    public async Task Live_is_empty_when_the_folder_is_not_there() =>
        Assert.Empty(await new ProcessGroupSliceScopes(Path.Combine(_dir, "none")).Live(Token));

    private Process Session(string unit)
    {
        var setsid = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(d => Path.Combine(d, "setsid")).FirstOrDefault(File.Exists);
        Assert.SkipWhen(setsid is null, "setsid is not installed");
        var process = Process.Start(new ProcessStartInfo(setsid, ["sleep", "60"]) { UseShellExecute = false }) ?? throw new InvalidOperationException("setsid did not start");
        _started.Add(process);
        ProcessGroupFiles.WritePgid(_dir, unit, process.Id);   // why: setsid run from a non-leader execs in place, so the child's group id is its own pid
        return process;
    }
}
