using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Adapters;

/// <summary>MacProcessLiveness through a fake command runner and clock, so it runs on any OS.</summary>
public sealed class MacProcessLivenessTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static (MacProcessLiveness Liveness, InMemoryCommandRunner Runner) Make(Func<InMemoryCommandRunner, InMemoryCommandRunner> script)
    {
        var runner = script(new InMemoryCommandRunner());
        var clock = new FakeTimeProvider(_now);
        return (new MacProcessLiveness(new MacHostReader("/state", runner, MacToolPaths.Default, _ => 0), clock), runner);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_process_that_started_before_the_file_was_written_is_alive_and_ps_is_asked_for_that_pid()
    {
        var (liveness, runner) = Make(r => r.When("ps -o etime= -p 321", "   02:00:00\n"));

        var alive = await liveness.IsRunning(321, _now - TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.True(alive.Value);
        Assert.Equal(["/bin/ps -o etime= -p 321"], runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_process_that_started_after_the_file_was_written_is_a_reused_pid()
    {
        var (liveness, _) = Make(r => r.When("ps -o etime= -p 321", "   00:30\n"));

        var alive = await liveness.IsRunning(321, _now - TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        Assert.True(alive.IsSuccess);
        Assert.False(alive.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("01:05:00")]
    [InlineData("3-04:05:06")]
    public async Task The_elapsed_time_forms_ps_prints_are_read(string etime)
    {
        var (liveness, _) = Make(r => r.When("etime=", etime + "\n"));

        var alive = await liveness.IsRunning(5, _now, TestContext.Current.CancellationToken);

        Assert.True(alive.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pid_ps_does_not_list_is_not_alive()
    {
        var (liveness, _) = Make(r => r.When("etime=", "", exitCode: 1));

        var alive = await liveness.IsRunning(321, _now, TestContext.Current.CancellationToken);

        Assert.True(alive.IsSuccess);
        Assert.False(alive.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_pid_is_not_alive_and_ps_is_not_run(int pid)
    {
        var (liveness, runner) = Make(r => r);

        var alive = await liveness.IsRunning(pid, _now, TestContext.Current.CancellationToken);

        Assert.False(alive.Value);
        Assert.Empty(runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ps_that_cannot_run_is_AW_META_010_not_a_guess()
    {
        var (liveness, _) = Make(r => r);

        var alive = await liveness.IsRunning(321, _now, TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, alive.Error.Code);
        Assert.Contains("unavailable on macOS: ps could not be run", alive.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_elapsed_time_that_cannot_be_read_is_AW_META_010()
    {
        var (liveness, _) = Make(r => r.When("etime=", "soon\n"));

        var alive = await liveness.IsRunning(321, _now, TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, alive.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_batch_lists_the_processes_with_one_ps_and_answers_every_claim_in_order()
    {
        var (liveness, runner) = Make(r => r.When("-axo pid=,etime=", "  321    02:00:00\n  400       00:30\n"));

        var answers = await liveness.Running(
            [new ProcessClaim(321, _now - TimeSpan.FromMinutes(5)), new ProcessClaim(400, _now - TimeSpan.FromHours(1)), new ProcessClaim(999, _now), new ProcessClaim(0, _now)],
            TestContext.Current.CancellationToken);

        Assert.Equal([true, false, false, false], answers.Select(a => a.Value));
        Assert.Equal(["/bin/ps -axo pid=,etime="], runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_batch_whose_ps_cannot_run_is_AW_META_010_for_every_claim_not_an_exception()
    {
        var (liveness, _) = Make(r => r);

        var answers = await liveness.Running([new ProcessClaim(321, _now), new ProcessClaim(400, _now)], TestContext.Current.CancellationToken);

        Assert.All(answers, a => Assert.Equal(MetaErrors.HostUnreadable.Code, a.Error.Code));
        Assert.Equal(2, answers.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_clock_is_read_before_ps_runs_so_a_slow_ps_cannot_make_a_live_process_look_reused()
    {
        var runner = new InMemoryCommandRunner();
        var clock = new FakeTimeProvider(_now);
        _ = runner.When("etime=", _ =>
        {
            clock.Advance(TimeSpan.FromMinutes(10));   // ps took ten minutes (a loaded host)
            return new CommandResult(0, "   01:00\n", string.Empty, TimedOut: false);
        });
        var liveness = new MacProcessLiveness(new MacHostReader("/state", runner, MacToolPaths.Default, _ => 0), clock);

        // the file was written 30 s before the call; the process is a minute old when ps looks at it: it started before the file
        var alive = await liveness.IsRunning(321, _now - TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(alive.Value);
    }
}
