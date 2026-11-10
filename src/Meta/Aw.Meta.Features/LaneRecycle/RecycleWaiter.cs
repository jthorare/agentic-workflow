using Aw.Kernel;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>
/// The recycle of a lane whose own turn runs the command (the meta): <see cref="Run"/> starts a detached waiter (--detach), or is the waiter (--wait-idle) that waits for the
/// lane's stop beat, then recycles. Red stub (#2498): the behavior is not written yet.
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
    internal ValueTask<Result<LaneRecycleResponse>> Run(RecyclePlan plan, LaneRecycleRequest request, CancellationToken cancellationToken)
    {
        _ = (launcher, files, liveness, beats, runner, log, clock, plan, request, cancellationToken);
        throw new NotSupportedException("red stub (#2498): the recycle waiter is not written yet");
    }
}
