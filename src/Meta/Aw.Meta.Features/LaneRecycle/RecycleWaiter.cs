using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>
/// The recycle of a lane whose own turn runs the command (the meta): <see cref="Run"/> starts a detached waiter (--detach), or is the waiter (--wait-idle) that waits for the
/// lane's stop beat, then recycles. The waiter's pid is kept in the lane records; a dead pid does not count, and every exit of the waiter clears it.
/// </summary>
/// <param name="launcher">Starts the detached waiter.</param>
/// <param name="files">The lane records, which keep the waiter's pid.</param>
/// <param name="liveness">Tells a live waiter pid from a dead one.</param>
/// <param name="beats">The lanes' stop beats, which also wait between polls.</param>
/// <param name="runner">The recycle itself.</param>
/// <param name="log">The running log.</param>
/// <param name="clock">The current time.</param>
internal sealed class RecycleWaiter(IWaiterLauncher launcher, ILaneFiles files, IProcessLiveness liveness, LaneBeats beats, RecycleRunner runner, IRunningLog log, TimeProvider clock)
{
    internal static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(30);

    // The logical program name: the launcher runs the current aw-meta binary in its place.
    internal const string Program = "aw-meta";

    internal ValueTask<Result<LaneRecycleResponse>> Run(RecyclePlan plan, LaneRecycleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(request);
        return request.WaitIdle ? WaitAndRecycle(plan, cancellationToken) : Detach(plan, cancellationToken);
    }

    private async ValueTask<Result<LaneRecycleResponse>> Detach(RecyclePlan plan, CancellationToken cancellationToken)
    {
        var recorded = await files.Waiter(plan.Lane, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        if (recorded.Value is { } existing)
        {
            var running = await liveness.IsRunning(existing.Pid, existing.StartedAt, cancellationToken).ConfigureAwait(false);
            if (running.IsError || running.Value)
            {
                return MetaErrors.RecycleRefused.With($"a recycle waiter for lane {plan.Lane} is already running (pid {existing.Pid.ToString(CultureInfo.InvariantCulture)}); nothing was started");
            }
        }

        var startedAt = clock.GetUtcNow();
        var started = await launcher.Start([Program, "lane-recycle", plan.Lane, "--digest", plan.Digest, "--wait-idle"], cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            return started.Error;
        }

        var kept = await files.RecordWaiter(plan.Lane, (started.Value, startedAt), cancellationToken).ConfigureAwait(false);
        if (kept.IsError)
        {
            return kept.Error;
        }

        return new LaneRecycleResponse(plan.Lane, DryRun: false, plan.OldSession.Value, NewSession: null, plan.OldTokens, plan.Digest, DetachSteps(plan), Warning: null, started.Value);
    }

    private static IReadOnlyList<string> DetachSteps(RecyclePlan plan) =>
    [
        $"started a detached waiter (aw-meta lane-recycle {plan.Lane} --wait-idle): it waits up to 30 min for the next turn of lane {plan.Lane} to end, then recycles",
        .. RecycleRunner.Steps(plan),
    ];

    private async ValueTask<Result<LaneRecycleResponse>> WaitAndRecycle(RecyclePlan plan, CancellationToken cancellationToken)
    {
        try
        {
            var kept = await files.Waiter(plan.Lane, cancellationToken).ConfigureAwait(false);
            var since = kept.IsSuccess && kept.Value is { } mine ? mine.StartedAt : clock.GetUtcNow();
            var idle = await WaitForStop(plan.Lane, since, cancellationToken).ConfigureAwait(false);
            if (idle.IsError)
            {
                return idle.Error;
            }

            if (!idle.Value)
            {
                return await GaveUp(plan, cancellationToken).ConfigureAwait(false);
            }

            return await runner.Run(plan, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = await files.RecordWaiter(plan.Lane, waiter: null, CancellationToken.None).ConfigureAwait(false);
        }
    }

    // True once a stop beat newer than <since> arrives; false when 30 minutes after <since> pass first.
    private ValueTask<Result<bool>> WaitForStop(string lane, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var left = since + GiveUpAfter - clock.GetUtcNow();
        return beats.WaitForStop(lane, since, left > TimeSpan.Zero ? left : TimeSpan.Zero, cancellationToken);
    }

    private async ValueTask<Result<LaneRecycleResponse>> GaveUp(RecyclePlan plan, CancellationToken cancellationToken)
    {
        var note = LogLine.Event(LogKind.Note, clock.GetUtcNow(), $"recycle of {plan.Lane} gave up: no turn ended within 30 min; an open obligation keeps the stop blocked");
        var logged = await log.Append(note, cancellationToken).ConfigureAwait(false);
        return logged.IsError
            ? logged.Error
            : MetaErrors.RecycleRefused.With($"recycle of {plan.Lane} gave up: no turn ended within 30 min; nothing was typed");
    }
}
