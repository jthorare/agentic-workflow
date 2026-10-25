using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// The one clock and the memory watch of a sandboxed session (sandbox spec §3.3, SB7, SB16). On every tick it reads the scope's counters (keeping the peaks, since the files go
/// with the scope), asks the engine whether the run is suspended (a failed answer counts as not suspended, so a runaway is still stopped),
/// counts only unsuspended time against the wall limit, moves the scope's loose ceiling forward when a suspension ends, and stops the session when the wall is used up:
/// SIGTERM, ten seconds, then SIGKILL. A memory kill is the kernel's; the watchdog sees it in memory.events, or after the scope in systemd's Result.
/// </summary>
/// <param name="limits">The session's limits.</param>
/// <param name="scope">The session's scope.</param>
/// <param name="calls">The control plane calls (the suspension read).</param>
/// <param name="clock">The time the ticks and the grace run on.</param>
/// <param name="errors">Where wrap writes its own messages.</param>
internal sealed class LimitWatchdog(WrapLimits limits, IScopeControl scope, IWrapCalls calls, TimeProvider clock, TextWriter errors)
{
    /// <summary>How often the scope and the suspension are read (a memory limit shows within this).</summary>
    internal static readonly TimeSpan Tick = TimeSpan.FromSeconds(5);

    /// <summary>How long a SIGTERM may take before the scope is killed outright.</summary>
    internal static readonly TimeSpan TermGrace = TimeSpan.FromSeconds(10);

    private readonly WallBudget _budget = new(limits.WallSeconds is { } wall ? TimeSpan.FromSeconds(wall) : TimeSpan.MaxValue);
    private LimitEnd? _verdict;
    private DateTimeOffset? _previousAt;
    private long _previousCpu;
    private long _peakMemory;
    private int _peakTasks;
    private int _peakCpuPercent;
    private bool _wasSuspended;
    private bool _sampleFailing;

    /// <summary>Watches the session until <paramref name="stop"/> or until the wall limit has stopped it.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The launch secret for the suspension read, or null (then nothing is ever suspended).</param>
    /// <param name="childExited">Completes when the agent exits.</param>
    /// <param name="stop">Ends the watch.</param>
    /// <returns>A task that completes when the watch ends.</returns>
    internal async Task Run(LaunchId launch, LaunchSecret? proof, Task childExited, CancellationToken stop)
    {
        var started = clock.GetUtcNow();
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var now = clock.GetUtcNow();
                await ReadScope(now, stop).ConfigureAwait(false);
                var suspended = await IsSuspended(launch, proof, stop).ConfigureAwait(false);
                _budget.Tick(now, suspended);
                await MoveCeiling(now - started, suspended, stop).ConfigureAwait(false);
                if (limits.WallSeconds is { } wall && _budget.Exceeded && _verdict is null)
                {
                    _verdict = new LimitEnd(LimitKind.Wall, wall, Seconds(_budget.Used));
                    await errors.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"sandbox: the wall limit ({TimeSpan.FromSeconds(wall):c}, suspended time not counted) is used up; stopping the session")).ConfigureAwait(false);
                    await Terminate(childExited, stop).ConfigureAwait(false);
                    return;
                }

                await Task.Delay(Tick, clock, stop).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // the agent exited, or wrap is stopping
        }
    }

    /// <summary>Tells whether a sandbox limit ended the session, once the agent has exited.</summary>
    /// <param name="cancellationToken">Cancels the read of the failed scope.</param>
    /// <returns>The limit, its value and the peaks, or null for an ordinary exit.</returns>
    internal async ValueTask<LimitEnd?> Conclude(CancellationToken cancellationToken)
    {
        if (_verdict is null)
        {
            _verdict = await scope.Result(cancellationToken).ConfigureAwait(false) switch
            {
                "oom-kill" => new LimitEnd(LimitKind.Memory, limits.MemoryMaxBytes, _peakMemory),   // the poll missed it: systemd still knows the failed unit
                "timeout" => new LimitEnd(LimitKind.Wall, limits.WallSeconds ?? 0, Seconds(_budget.Used)),   // the loose ceiling ended a scope the watchdog did not stop
                _ => null,
            };
        }

        return _verdict is { } found ? found with { Peaks = Peaks() } : null;
    }

    private LimitPeaks Peaks() => new(_peakMemory, _peakCpuPercent, _peakTasks, Seconds(_budget.Used));

    private static long Seconds(TimeSpan span) => (long)span.TotalSeconds;

    // The files go with the scope, so the last reading and the peaks are kept here; a scope that is gone (null) changes nothing.
    private async Task ReadScope(DateTimeOffset now, CancellationToken stop)
    {
        ScopeSample? sample;
        try
        {
            sample = await scope.Sample(stop).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: an unreadable cgroup must not stop the watch
        {
            if (!_sampleFailing)
            {
                _sampleFailing = true;
                await errors.WriteLineAsync($"sandbox: the scope could not be read: {e.Message}").ConfigureAwait(false);
            }

            return;
        }

        if (sample is null)
        {
            return;
        }

        _peakMemory = Math.Max(_peakMemory, Math.Max(sample.MemoryBytes, sample.MemoryPeakBytes));
        _peakTasks = Math.Max(_peakTasks, sample.Tasks);
        if (_previousAt is { } before && now > before && sample.CpuMicroseconds >= _previousCpu)
        {
            _peakCpuPercent = Math.Max(_peakCpuPercent, (int)Math.Round((sample.CpuMicroseconds - _previousCpu) / (now - before).TotalMicroseconds * 100));
        }

        (_previousAt, _previousCpu) = (now, sample.CpuMicroseconds);
        if (sample.Events.OomKill > 0 && _verdict is null)
        {
            _verdict = new LimitEnd(LimitKind.Memory, limits.MemoryMaxBytes, _peakMemory);
        }
    }

    // A failed or unreachable answer counts as not suspended, so a runaway is still stopped while serve is down (sandbox spec SB7).
    private async Task<bool> IsSuspended(LaunchId launch, LaunchSecret? proof, CancellationToken stop)
    {
        if (proof is not { } secret)
        {
            return false;
        }

        try
        {
            var asked = await calls.Suspended(launch, secret, stop).ConfigureAwait(false);
            return asked.IsSuccess && asked.Value;
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: an unreachable control plane means "not suspended"
        {
            return false;
        }
    }

    // When a park, an answer or a handback ends a suspension, the loose ceiling moves to what the run still needs (sandbox spec SB7, R3-03).
    private async Task MoveCeiling(TimeSpan elapsed, bool suspended, CancellationToken stop)
    {
        var resumed = _wasSuspended && !suspended;
        _wasSuspended = suspended;
        if (resumed && limits.WallSeconds is { } wall)
        {
            await scope.SetRuntimeMax(RuntimeCeiling.Next(elapsed, TimeSpan.FromSeconds(wall), _budget.Used, TimeSpan.FromSeconds(limits.ParkMaxSeconds)), stop).ConfigureAwait(false);
        }
    }

    // SIGTERM first; an agent that ignores it for ten seconds is killed with everything in the scope.
    private async Task Terminate(Task childExited, CancellationToken stop)
    {
        await scope.Kill(force: false, stop).ConfigureAwait(false);
        var grace = Task.Delay(TermGrace, clock, stop);
        if (await Task.WhenAny(childExited, grace).ConfigureAwait(false) == grace)
        {
            await scope.Kill(force: true, stop).ConfigureAwait(false);
        }
    }
}
