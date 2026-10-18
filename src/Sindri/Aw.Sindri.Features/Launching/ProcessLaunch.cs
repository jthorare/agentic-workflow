using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// Launches one request (UI spec E-1, E-6; caller 'service:launcher'). It takes the item's active instances over first, then claims the request (only while
/// at least the claim reserve of its deadline remains), re-checks nothing it has not just read, and calls 'ILauncher.Launch' under its limit. The outcome goes
/// under the shared 'launch.outcome' key: a start that loses the key to the expiry stops the session it just made. A request is never retried: whatever
/// happens here is the outcome. An attach goes to <see cref="AttachFlow"/>.
/// </summary>
/// <param name="outcomes">Appends the outcome.</param>
/// <param name="takeover">The E-6 wait.</param>
/// <param name="launcher">Starts the session.</param>
/// <param name="attach">Attaches to a live session.</param>
/// <param name="plans">Builds the launch.</param>
/// <param name="work">What the launcher remembers.</param>
/// <param name="timings">The limits.</param>
internal sealed class ProcessLaunch(LaunchOutcomes outcomes, TakeoverWait takeover, ILauncher launcher, AttachFlow attach, LaunchPlans plans, LaunchWork work, LaunchTimings timings)
    : IService<ProcessLaunchRequest, ProcessLaunchResponse>
{
    public async ValueTask<Result<ProcessLaunchResponse>> Handle(ProcessLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pending = request.Pending;
        var launch = pending.Request.Launch;
        try
        {
            var done = pending.Request.Mode == LaunchMode.Attach
                ? await attach.Run(request.Grant, pending, cancellationToken).ConfigureAwait(false)
                : await Open(request.Grant, pending, cancellationToken).ConfigureAwait(false);
            return done.IsError ? done.Error : new ProcessLaunchResponse(done.Value);
        }
        finally
        {
            work.Release(launch);   // handed out once, never again; if it ended with no outcome (the views weren't ready) the sweep gives it one
        }
    }

    private async ValueTask<Result<bool>> Open(LeaseGrant grant, PendingLaunch pending, CancellationToken cancellationToken)
    {
        var waited = await takeover.Run(pending, cancellationToken).ConfigureAwait(false);
        if (waited.IsError)
        {
            return await Fail(grant, pending, waited.Error, stage: null, cancellationToken).ConfigureAwait(false);   // the engine's code
        }

        if (!waited.Value)
        {
            return false;
        }

        var plan = await plans.Build(pending, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return await Fail(grant, pending, plan.Error, stage: null, cancellationToken).ConfigureAwait(false);
        }

        return work.TryClaim(pending, timings)
            ? await Launch(grant, pending, plan.Value, cancellationToken).ConfigureAwait(false)
            : false;   // too little of the deadline is left, or the sweep already failed it
    }

    private async ValueTask<Result<bool>> Launch(LeaseGrant grant, PendingLaunch pending, LaunchRequest plan, CancellationToken cancellationToken)
    {
        using var bound = new CancellationTokenSource(timings.LaunchLimit, timings.Clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bound.Token);
        LaunchResult result;
        try
        {
            result = await launcher.Launch(plan, linked.Token).AsTask().WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _ = await launcher.Stop(plan.Launch, CancellationToken.None).ConfigureAwait(false);   // a launch that finishes late must leave no session
            return await Fail(grant, pending, SindriErrors.LaunchTimedOut.With("the launcher did not answer within its limit (a tab may have opened: run `tmux -L sindri-launch ls`)"), stage: null, CancellationToken.None).ConfigureAwait(false);
        }

        return result is StartedLaunch started
            ? await Record(grant, plan, started, cancellationToken).ConfigureAwait(false)
            : await Fail(grant, pending, ((FailedLaunch)result).Error, ((FailedLaunch)result).Stage, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<bool>> Record(LeaseGrant grant, LaunchRequest plan, StartedLaunch started, CancellationToken cancellationToken)
    {
        var appended = await outcomes.Started(grant, plan, started, cancellationToken).ConfigureAwait(false);
        if (appended.IsError || appended.Value.AlreadyRecorded)
        {
            _ = await launcher.Stop(plan.Launch, CancellationToken.None).ConfigureAwait(false);   // the expiry got the outcome first: nobody was told this session exists
            return appended.IsError ? appended.Error : false;
        }

        if (started.Viewer.Kind == ViewerOutcomeKind.Unconfirmed && started.Host.Kind == HostKind.Tmux)
        {
            work.Watch(new WatchedViewer(plan.Launch, plan.Item, started.Host, timings.Clock.GetUtcNow() + timings.Deadline));
        }

        return true;
    }

    private async ValueTask<Result<bool>> Fail(LeaseGrant grant, PendingLaunch pending, Error error, LaunchStage? stage, CancellationToken cancellationToken)
    {
        work.ProbeSoon();   // what a launch could not do may be what the box lost: the next tick checks the targets again (E-3)
        var failed = new LaunchFailed(pending.Request.Launch, pending.Request.Item, error.Code, error.Message, stage, SandboxRefusal.ReasonOf(error), SandboxRefusal.SourceTextOf(error));
        var appended = await outcomes.Failed(grant, failed, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : false;
    }
}
