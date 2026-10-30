using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// The only clock of a sandboxed session (sandbox spec §3.3; SB-3a): one tick every 5 s reads the scope's accounting, asks the control plane whether the run is
/// suspended, counts unsuspended time and, when the wall is used up, ends the scope with TERM, then KILL 10 s later. It also tells a memory kill from any other end.
/// </summary>
/// <param name="scope">systemd's view of the scope.</param>
/// <param name="stats">The scope's accounting files.</param>
/// <param name="calls">The control plane calls (the suspension read).</param>
/// <param name="clock">The time the ticks run on.</param>
/// <param name="limits">The limits file.</param>
/// <param name="memoryKills">Where the watchdog writes down a memory kill. Non-null means this platform has no kernel memory limit (macOS): the watchdog itself kills the scope on the first tick that reads the memory line reached, and ends the session as a memory limit. Null leaves the kill to the kernel, which is what Linux does.</param>
internal sealed class LimitWatchdog(IScopeControl scope, ICgroupStats stats, IWrapCalls calls, TimeProvider clock, WrapLimits limits, IMemoryKills? memoryKills = null)
{
    private static readonly TimeSpan _every = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _killGrace = TimeSpan.FromSeconds(10);
    private readonly DateTimeOffset _start = clock.GetUtcNow();
    private readonly WallBudget _budget = new(limits.WallSeconds is { } wall ? TimeSpan.FromSeconds(wall) : TimeSpan.MaxValue);
    private bool _wallHit;
    private bool _memoryHit;
    private string? _cgroup;
    private CgroupSample? _last;
    private DateTimeOffset? _lastAt;
    private long _oomKills;
    private long _memoryPeak;
    private int _tasksPeak;
    private int _cpuPeak;
    private long _pidsHits;
    private bool _pidsRecorded;

    /// <summary>Watches until the agent exits.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The launch secret, or null when wrap has none (then nothing is ever suspended).</param>
    /// <param name="childExited">Completes when the agent exits.</param>
    /// <param name="cancellationToken">Cancels the watch.</param>
    /// <returns>The limit that ended the session, or null when none did.</returns>
    internal async Task<LimitEnd?> Run(LaunchId launch, LaunchSecret? proof, Task childExited, CancellationToken cancellationToken)
    {
        using var done = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            while (!childExited.IsCompleted)
            {
                await Tick(launch, proof, childExited, done.Token).ConfigureAwait(false);
                _ = await Task.WhenAny(childExited, Task.Delay(_every, clock, done.Token)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // wrap is being cancelled: report what is known
        }
        finally
        {
            await done.CancelAsync().ConfigureAwait(false);   // the tick delay that lost the race
        }

        return await Verdict().ConfigureAwait(false);
    }

    private async Task Tick(LaunchId launch, LaunchSecret? proof, Task childExited, CancellationToken token)
    {
        var now = clock.GetUtcNow();
        await Sample(now, token).ConfigureAwait(false);
        await KillAtMemoryMax(token).ConfigureAwait(false);
        await RecordPidsHit(launch, proof, token).ConfigureAwait(false);
        var resumed = _budget.Tick(now, await IsSuspended(launch, proof, token).ConfigureAwait(false));
        if (limits.WallSeconds is null)
        {
            return;   // an attended session has no wall and no ceiling
        }

        if (resumed)
        {
            _ = await scope.SetRuntimeMax(limits.Unit, RuntimeCeiling.Next(now - _start, _budget.Left, TimeSpan.FromSeconds(limits.ParkMaxSeconds)), token).ConfigureAwait(false);
        }

        if (!_wallHit && _budget.Exhausted)
        {
            _wallHit = true;
            await EndScope(childExited, token).ConfigureAwait(false);
        }
    }

    private async Task<LimitEnd?> Verdict()
    {
        var peaks = new LimitPeaks(_memoryPeak, _cpuPeak, _tasksPeak, (long)_budget.Used.TotalSeconds);
        if (_wallHit)
        {
            return new LimitEnd(LimitKind.Wall, limits.WallSeconds!.Value, peaks.WallSeconds, peaks);   // why: the wall is only hit when it is set
        }

        if (_memoryHit || _oomKills > 0 || string.Equals(await UnitResult().ConfigureAwait(false), "oom-kill", StringComparison.Ordinal))
        {
            // a poll that missed the kill leaves no peak: the kernel killed at the line, so the line is the best figure
            return new LimitEnd(LimitKind.Memory, limits.MemoryMaxBytes, _memoryPeak > 0 ? _memoryPeak : limits.MemoryMaxBytes, peaks);
        }

        return null;
    }

    private async Task KillAtMemoryMax(CancellationToken token)
    {
        if (memoryKills is null || _memoryHit || _last is not { } sample || limits.MemoryMaxBytes <= 0 || sample.MemoryCurrent < limits.MemoryMaxBytes)
        {
            return;
        }

        _memoryHit = true;
        _ = await scope.Kill(limits.Unit, "SIGKILL", token).ConfigureAwait(false);
        _ = await memoryKills.Record(limits.Unit, token).ConfigureAwait(false);
    }

    private async Task EndScope(Task childExited, CancellationToken token)
    {
        _ = await scope.Kill(limits.Unit, "SIGTERM", token).ConfigureAwait(false);
        var grace = Task.Delay(_killGrace, clock, token);
        if (await Task.WhenAny(childExited, grace).ConfigureAwait(false) == grace)
        {
            _ = await scope.Kill(limits.Unit, "SIGKILL", token).ConfigureAwait(false);
        }
    }

    private async Task<bool> IsSuspended(LaunchId launch, LaunchSecret? proof, CancellationToken token)
    {
        if (proof is not { } secret)
        {
            return false;
        }

        try
        {
            var answer = await calls.Suspended(launch, secret, token).ConfigureAwait(false);
            return answer.IsSuccess && answer.Value;   // an error counts as not suspended: a runaway is still stopped while serve is down
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>The first tick that sees a refused fork tells the control plane once (never kills); a failed call is tried again next tick.</summary>
    private async Task RecordPidsHit(LaunchId launch, LaunchSecret? proof, CancellationToken token)
    {
        if (_pidsRecorded || _pidsHits == 0 || proof is not { } secret)
        {
            return;
        }

        try
        {
            var sent = await calls.LimitHit(launch, secret, LimitHitKind.Pids, _tasksPeak, token).ConfigureAwait(false);
            _pidsRecorded = sent.IsSuccess;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // why: a record of a limit is a fact for the reader, never a reason to stop the watch; the next tick tries again
        }
    }

    private async Task<string?> UnitResult()
    {
        try
        {
            return await scope.Result(limits.Unit, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

    private async Task Sample(DateTimeOffset now, CancellationToken token)
    {
        _cgroup ??= await scope.ControlGroup(limits.Unit, token).ConfigureAwait(false);
        if (_cgroup is null || stats.Read(_cgroup) is not { } sample)
        {
            return;   // the scope is not there (yet, or any more): the last sample stands
        }

        _oomKills = Math.Max(_oomKills, sample.OomKills);
        _memoryPeak = Math.Max(_memoryPeak, Math.Max(sample.MemoryPeak, sample.MemoryCurrent));
        _tasksPeak = Math.Max(_tasksPeak, Math.Max(sample.Pids, (int)Math.Min(sample.PidsPeak, int.MaxValue)));
        _pidsHits = Math.Max(_pidsHits, sample.PidsMax);
        if (_last is { } before && _lastAt is { } at && now > at && sample.CpuMicroseconds >= before.CpuMicroseconds)
        {
            var percent = (sample.CpuMicroseconds - before.CpuMicroseconds) * 100 / (now - at).TotalMicroseconds;
            _cpuPeak = Math.Max(_cpuPeak, (int)Math.Round(percent, MidpointRounding.AwayFromZero));
        }

        _last = sample;
        _lastAt = now;
    }
}
