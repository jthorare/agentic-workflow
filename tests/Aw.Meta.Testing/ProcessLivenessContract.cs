using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IProcessLiveness contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class ProcessLivenessContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>The liveness check under test.</summary>
    /// <returns>The check.</returns>
    protected abstract IProcessLiveness Create();

    /// <summary>Starts a long-running process, now.</summary>
    /// <returns>Its pid.</returns>
    protected abstract int StartProcess();

    /// <summary>Ends a process started by <see cref="StartProcess"/> and waits until it is gone.</summary>
    /// <param name="pid">Its pid.</param>
    protected abstract void StopProcess(int pid);

    /// <summary>A running process is alive for a file written after it started.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_running_process_is_alive_for_a_file_written_after_it_started()
    {
        var liveness = Create();
        var pid = StartProcess();

        var alive = await liveness.IsRunning(pid, RealTime.NowOffset, _none);

        Expect.That(alive.IsSuccess && alive.Value, "running, started before the file was written");
    }

    /// <summary>A pid whose process started after the file was written is another process.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_process_that_started_after_the_file_was_written_is_a_reused_pid()
    {
        var liveness = Create();
        var pid = StartProcess();

        var alive = await liveness.IsRunning(pid, RealTime.NowOffset - TimeSpan.FromHours(1), _none);

        Expect.That(alive.IsSuccess && !alive.Value, "started an hour after the file was written: not its owner");
    }

    /// <summary>A process that has exited is not alive.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_process_that_has_exited_is_not_alive()
    {
        var liveness = Create();
        var pid = StartProcess();
        StopProcess(pid);

        var alive = await liveness.IsRunning(pid, RealTime.NowOffset, _none);

        Expect.That(alive.IsSuccess && !alive.Value, "gone");
    }

    /// <summary>A batch answers every claim in order, as the single call does for it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_batch_answers_each_claim_in_order_as_the_single_call_does()
    {
        var liveness = Create();
        var pid = StartProcess();
        var gone = StartProcess();
        StopProcess(gone);

        var answers = await liveness.Running(
            [
                new ProcessClaim(pid, RealTime.NowOffset),
                new ProcessClaim(pid, RealTime.NowOffset - TimeSpan.FromHours(1)),
                new ProcessClaim(gone, RealTime.NowOffset),
                new ProcessClaim(2_147_000_000, RealTime.NowOffset),
                new ProcessClaim(0, RealTime.NowOffset),
            ],
            _none);

        Expect.That(answers.Count == 5 && answers.All(a => a.IsSuccess), "every claim is answered");
        Expect.That(answers.Select(a => a.Value).SequenceEqual([true, false, false, false, false]), "running; reused; exited; no such pid; not a pid");
    }

    /// <summary>A pid no process has, or that is not a pid, is not alive.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_pid_no_process_has_is_not_alive()
    {
        var liveness = Create();

        foreach (var pid in new[] { 0, -5, 2_147_000_000 })
        {
            var alive = await liveness.IsRunning(pid, RealTime.NowOffset, _none);
            Expect.That(alive.IsSuccess && !alive.Value, "pid " + pid.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is not running");
        }
    }
}
