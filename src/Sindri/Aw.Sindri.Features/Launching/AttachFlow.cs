using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// Attach (UI spec E-5, "attach when live"): brings the viewer up on the item's live session and starts no process. The launcher service resolves the
/// session from the launches view; with none live the request goes to the launcher, which answers SND-LAUNCH-006. The attach is itself a launch, so it
/// records 'launch.started' (mode attach) with the live session's argv, or 'launch.failed' when the viewer failed.
/// </summary>
/// <param name="launches">The item's launches.</param>
/// <param name="viewer">Opens the viewer.</param>
/// <param name="launcher">Answers a request with no live session to attach to.</param>
/// <param name="plans">Builds that request.</param>
/// <param name="outcomes">Appends the outcome.</param>
/// <param name="timings">The limits.</param>
internal sealed class AttachFlow(ILaunchViews launches, ITerminalViewer viewer, ILauncher launcher, LaunchPlans plans, LaunchOutcomes outcomes, LaunchTimings timings)
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

        var live = rows.Value.FirstOrDefault(static row => row.Mode != LaunchMode.Attach && row.Live && row.Attach is { Count: > 0 });
        return live is { Attach: { } argv }
            ? await Show(grant, request, live, argv, cancellationToken).ConfigureAwait(false)
            : await NoSession(grant, pending, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<bool>> NoSession(LeaseGrant grant, PendingLaunch pending, CancellationToken cancellationToken)
    {
        var plan = await plans.Build(pending, cancellationToken).ConfigureAwait(false);
        var error = plan.IsError ? plan.Error : (await launcher.Launch(plan.Value, cancellationToken).ConfigureAwait(false)) is FailedLaunch failed ? failed.Error : SindriErrors.LaunchNoSession.With("the item has no live session");
        return await Fail(grant, pending.Request, error, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<bool>> Show(LeaseGrant grant, ItemOpenRequested request, LaunchRow live, IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        using var bound = new CancellationTokenSource(timings.LaunchLimit, timings.Clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bound.Token);
        ViewerOutcome shown;
        try
        {
            var host = new HostRef(HostKind.Tmux, LaunchSessionName.For(live.Id));
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

        var started = new LaunchStarted(request.Launch, request.Item, LaunchMode.Attach, live.Target, live.SessionId, live.TabTitle ?? LaunchOutcomes.TabTitle(request.Item), outcomes.ViewerOf(shown),
            argv, live.CatalogSource ?? CatalogSource.Builtin, live.ArgvHash ?? string.Empty);
        var appended = await outcomes.Started(grant, started, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : !appended.Value.AlreadyRecorded;
    }

    private async ValueTask<Result<bool>> Fail(LeaseGrant grant, ItemOpenRequested request, Error error, CancellationToken cancellationToken)
    {
        var appended = await outcomes.Failed(grant, new LaunchFailed(request.Launch, request.Item, error.Code, error.Message, Stage: null), cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : false;
    }
}
