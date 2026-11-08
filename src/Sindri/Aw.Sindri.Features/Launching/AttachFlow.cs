using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// Attach (UI spec E-5, "attach when live"): brings the viewer up on the item's live session and starts no process. The launcher service resolves the
/// session from the launches view; with none live the request fails at once as SND-LAUNCH-006 (the launcher is not asked). The attach is itself a launch, so it
/// records 'launch.started' (mode attach) with the live session's argv, or 'launch.failed' when the viewer failed.
/// </summary>
/// <param name="launches">The item's launches.</param>
/// <param name="viewer">Opens the viewer.</param>
/// <param name="outcomes">Appends the outcome.</param>
/// <param name="work">Where an 'unconfirmed' viewer is watched for its client.</param>
/// <param name="timings">The limits.</param>
internal sealed class AttachFlow(ILaunchViews launches, ITerminalViewer viewer, LaunchOutcomes outcomes, LaunchWork work, LaunchTimings timings)
{
    private static readonly TimeSpan _window = TimeSpan.FromSeconds(3);

    /// <summary>Attaches.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="pending">The attach request.</param>
    /// <param name="cancellationToken">Cancels the attach.</param>
    /// <returns>True when 'launch.started' was recorded.</returns>
    public async ValueTask<Result<bool>> Run(LeaseGrant grant, PendingLaunch pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var request = pending.Request;
        var rows = await launches.ForItem(request.Item, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var live = rows.Value.FirstOrDefault(static row => row.Live && row.Attach is { Count: > 0 });
        return live is { Attach: { } argv }
            ? await Show(grant, request, live, argv, cancellationToken).ConfigureAwait(false)
            : await NoSession(grant, pending, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<bool>> NoSession(LeaseGrant grant, PendingLaunch pending, CancellationToken cancellationToken) =>
        await Fail(grant, pending.Request, SindriErrors.LaunchNoSession.With("the item has no live session (fix: open it instead)"), cancellationToken).ConfigureAwait(false);

    private async ValueTask<Result<bool>> Show(LeaseGrant grant, ItemOpenRequested request, LaunchRow live, IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        using var bound = new CancellationTokenSource(timings.LaunchLimit, timings.Clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bound.Token);
        ViewerOutcome shown;
        var host = new HostRef(HostKind.Tmux, LaunchSessionName.For(live.Id));
        try
        {
            shown = await viewer.Open(new ViewerRequest(host, argv, LaunchOutcomes.TabTitle(request.Item), _window), linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await Fail(grant, request, SindriErrors.LaunchTimedOut.With("the viewer did not return in time"), cancellationToken).ConfigureAwait(false);
        }

        if (shown.Kind == ViewerOutcomeKind.Failed)
        {
            return await Fail(grant, request, SindriErrors.LaunchViewerFailed.With(shown.Detail ?? "the viewer failed"), cancellationToken).ConfigureAwait(false);
        }

        var started = new LaunchStarted(request.Launch, request.Item, LaunchMode.Attach, live.Target, live.SessionId, live.TabTitle ?? LaunchOutcomes.TabTitle(request.Item),
            await outcomes.ViewerOf(shown, attended: true, cancellationToken).ConfigureAwait(false),
            argv, live.CatalogSource ?? CatalogSource.Builtin, live.ArgvHash ?? string.Empty);
        var appended = await outcomes.AppendStarted(grant, started, cancellationToken).ConfigureAwait(false);
        if (appended.IsSuccess && !appended.Value.AlreadyRecorded && shown.Kind == ViewerOutcomeKind.Unconfirmed)
        {
            work.Watch(new WatchedViewer(request.Launch, request.Item, host, timings.Clock.GetUtcNow() + timings.Deadline));   // a client that attaches later turns 'unconfirmed' into 'opened'
        }

        return appended.IsError ? appended.Error : !appended.Value.AlreadyRecorded;
    }

    private async ValueTask<Result<bool>> Fail(LeaseGrant grant, ItemOpenRequested request, Error error, CancellationToken cancellationToken)
    {
        var appended = await outcomes.Failed(grant, new LaunchFailed(request.Launch, request.Item, error.Code, error.Message, LaunchFailureDetail.Of(error, stage: null)), cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : false;
    }
}
